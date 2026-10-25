using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace jaytwo.Pdf.Internal;

// Prunes the field tree to retained widgets, splitting merged fields from their page-owned widget copies.
internal sealed class PdfAssemblyForms
{
    private static readonly HashSet<string> FieldKeys = new HashSet<string>
    {
        "FT", "T", "TU", "TM", "Ff", "V", "DV", "DA", "Q", "DS", "RV", "MaxLen", "Opt", "TI", "I",
    };

    private readonly PdfObjectStore _store;
    private readonly PdfNewFileBudget _budget;
    private readonly PdfNewFileWriter _writer;
    private readonly PdfTypedObjectCopier _copier;
    private readonly Dictionary<PdfObject.Dict, PdfAssemblyField> _fields = new Dictionary<PdfObject.Dict, PdfAssemblyField>();
    private readonly Dictionary<int, PdfAssemblyField> _fieldIds = new Dictionary<int, PdfAssemblyField>();
    private readonly HashSet<int> _selectedIds = new HashSet<int>();
    private readonly HashSet<PdfObject.Dict> _selectedDirect = new HashSet<PdfObject.Dict>();
    private readonly HashSet<int> _pathIds = new HashSet<int>();
    private readonly HashSet<PdfObject.Dict> _pathDirect = new HashSet<PdfObject.Dict>();
    private readonly List<PdfAssemblyField> _roots = new List<PdfAssemblyField>();
    private readonly Dictionary<string, string> _fonts = new Dictionary<string, string>();
    private readonly PdfObject.Dict _outputForm;
    private readonly PdfObject.Dict? _sourceForm;
    private readonly string _namespace;

    internal PdfAssemblyForms(PdfObjectStore store, PdfNewFileBudget budget, PdfNewFileWriter writer, PdfTypedObjectCopier copier, PdfObject.Dict catalog, PdfObject.Dict outputForm, int occurrence, IEnumerable<PdfObject.Dict> selectedPages)
    {
        _store = store;
        _budget = budget;
        _writer = writer;
        _copier = copier;
        _outputForm = outputForm;
        _namespace = "Input" + occurrence.ToString(CultureInfo.InvariantCulture);
        foreach (PdfObject.Dict page in selectedPages)
        {
            if (page.Get("Annots") is not PdfObject annotations)
            {
                continue;
            }

            foreach (PdfObject annotation in Array(annotations).Items)
            {
                PdfObject.Dict widget = Dictionary(annotation);
                if (PdfValues.NameOf(_store.ResolveMaybe(widget.Get("Subtype"))) == "Widget")
                {
                    if (widget.ObjectNumber == 0)
                    {
                        _selectedDirect.Add(widget);
                    }
                    else
                    {
                        _selectedIds.Add(widget.ObjectNumber);
                    }
                }
            }
        }

        PdfObject? rawForm = catalog.Get("AcroForm");
        if (rawForm == null)
        {
            return;
        }

        _sourceForm = Dictionary(rawForm);
        if (_sourceForm.Get("CO") is PdfObject calculation
            && _store.Resolve(calculation) is not PdfObject.Arr { Items.Count: 0 })
        {
            throw Unsupported("calculation order");
        }

        if (_sourceForm.Get("Fields") is PdfObject fields)
        {
            foreach (PdfObject field in Array(fields).Items)
            {
                PdfAssemblyField? node = Read(field, null, 0);
                if (node != null)
                {
                    _roots.Add(node);
                }
            }
        }
    }

    internal PdfObject.Ref Widget(PdfObject.Dict source, PdfObject.Dict copy)
    {
        if (!_fields.TryGetValue(source, out PdfAssemblyField? node)
            && !_fieldIds.TryGetValue(source.ObjectNumber, out node))
        {
            throw Unsupported("a widget outside the AcroForm field tree");
        }

        _budget.AddStructuralBytes(64);
        PdfObject.Ref reference = _writer.Register(copy);
        node.Widgets.Add(copy);
        node.WidgetReferences.Add(reference);
        return reference;
    }

    internal void Complete(PdfObject.Arr parents)
    {
        if (!HasWidgets(_roots))
        {
            return;
        }

        Defaults();
        var parent = new PdfObject.Dict();
        parent.Set("T", new PdfObject.Str(Encoding.ASCII.GetBytes(_namespace), false));
        if (_sourceForm?.Get("DA") is PdfObject appearance)
        {
            parent.Set("DA", Appearance(appearance));
        }

        if (_sourceForm?.Get("Q") is PdfObject alignment)
        {
            parent.Set("Q", _copier.CopyDirect(_store, alignment, "resource"));
        }

        if (_store.ResolveMaybe(_sourceForm?.Get("NeedAppearances")) is PdfObject.Bool { Value: true })
        {
            _outputForm.Set("NeedAppearances", PdfObject.Bool.True);
        }

        PdfObject.Ref parentReference = _writer.Register(parent);
        var children = new PdfObject.Arr();
        foreach (PdfAssemblyField root in _roots)
        {
            PdfObject.Ref? reference = Write(root, parentReference);
            if (reference != null)
            {
                children.Items.Add(reference);
            }
        }

        parent.Set("Kids", children);
        parents.Items.Add(parentReference);
    }

    private static bool HasWidgets(IEnumerable<PdfAssemblyField> nodes)
    {
        foreach (PdfAssemblyField node in nodes)
        {
            if (node.Widgets.Count > 0 || HasWidgets(node.Children))
            {
                return true;
            }
        }

        return false;
    }

    private static PdfAssemblyNotSupportedException Unsupported(string detail)
    {
        return new PdfAssemblyNotSupportedException("Assembly cannot preserve " + detail + ".");
    }

    private PdfAssemblyField? Read(PdfObject raw, string? inheritedType, int depth)
    {
        if (depth > _budget.TraversalDepth)
        {
            throw new PdfResourceLimitException("field traversal depth");
        }

        using var release = _store.ReleaseAfter();
        PdfObject.Dict field = Dictionary(raw);
        string? type = PdfValues.NameOf(_store.ResolveMaybe(field.Get("FT"))) ?? inheritedType;
        if (type == "Sig")
        {
            return null;
        }

        bool added = field.ObjectNumber == 0 ? _pathDirect.Add(field) : _pathIds.Add(field.ObjectNumber);
        if (!added || _fields.ContainsKey(field) || _fieldIds.ContainsKey(field.ObjectNumber))
        {
            throw Unsupported("a cyclic or multiply owned field");
        }

        _budget.CountMappedObject();
        if (field.Get("A") != null || field.Get("AA") != null)
        {
            throw Unsupported("field actions or scripts");
        }

        var node = new PdfAssemblyField(field);
        bool selectedWidget = false;
        if (field.Get("Kids") is PdfObject kids)
        {
            foreach (PdfObject child in Array(kids).Items)
            {
                PdfObject.Dict dictionary = Dictionary(child);
                // A nameless widget kid belongs to this terminal field, even when a producer repeats /FT on it.
                if (PdfValues.NameOf(_store.ResolveMaybe(dictionary.Get("Subtype"))) == "Widget" && dictionary.Get("T") == null)
                {
                    if (dictionary.Get("A") != null || dictionary.Get("AA") != null || dictionary.Get("Kids") != null
                        || _fields.ContainsKey(dictionary) || _fieldIds.ContainsKey(dictionary.ObjectNumber)
                        || _pathIds.Contains(dictionary.ObjectNumber) || _pathDirect.Contains(dictionary))
                    {
                        throw Unsupported("widget actions or cyclic ownership");
                    }

                    _budget.CountMappedObject();
                    if (Selected(dictionary))
                    {
                        Remember(dictionary, node);
                        selectedWidget = true;
                    }

                    // A nameless widget is a leaf. Do not retain unselected widget objects in the parser cache.
                    if (dictionary.ObjectNumber != 0 && !_store.Dirty.Contains(dictionary.ObjectNumber))
                    {
                        _store.Cache.Remove(dictionary.ObjectNumber);
                    }
                }
                else
                {
                    PdfAssemblyField? nested = Read(child, type, depth + 1);
                    if (nested != null)
                    {
                        node.Children.Add(nested);
                    }
                }
            }
        }

        _pathIds.Remove(field.ObjectNumber);
        _pathDirect.Remove(field);
        if (!Selected(field) && node.Children.Count == 0 && !selectedWidget)
        {
            return null;
        }

        Remember(field, node);
        return node;
    }

    private bool Selected(PdfObject.Dict field)
    {
        return field.ObjectNumber == 0 ? _selectedDirect.Contains(field) : _selectedIds.Contains(field.ObjectNumber);
    }

    private void Remember(PdfObject.Dict field, PdfAssemblyField node)
    {
        _budget.AddStructuralBytes(64);
        if (field.ObjectNumber == 0)
        {
            _fields.Add(field, node);
        }
        else
        {
            _fieldIds.Add(field.ObjectNumber, node);
        }
    }

    private void Defaults()
    {
        PdfObject? resources = _sourceForm?.Get("DR");
        if (resources == null)
        {
            return;
        }

        PdfObject.Dict defaults = Dictionary(resources);
        new PdfAssemblyInspection(_store, _budget).Resources(defaults, 0);
        foreach (var entry in defaults.Items)
        {
            if (entry.Key is not ("Font" or "ProcSet"))
            {
                throw Unsupported("non-font default resources");
            }
        }

        if (defaults.Get("Font") is not PdfObject fonts)
        {
            return;
        }

        var outputResources = _outputForm.Get("DR") as PdfObject.Dict ?? new PdfObject.Dict();
        var outputFonts = outputResources.Get("Font") as PdfObject.Dict ?? new PdfObject.Dict();
        foreach (var font in Dictionary(fonts).Items)
        {
            string name = _namespace + "_" + _fonts.Count.ToString(CultureInfo.InvariantCulture);
            _fonts.Add(font.Key, name);
            outputFonts.Set(name, _copier.CopyDirect(_store, font.Value, "resource"));
        }

        outputResources.Set("Font", outputFonts);
        _outputForm.Set("DR", outputResources);
    }

    private PdfObject.Str Appearance(PdfObject value)
    {
        if (_store.Resolve(value) is not PdfObject.Str text)
        {
            throw Unsupported("an invalid default appearance");
        }

        // Reuse the PDF lexer, including escaped names and comments. Only text state and device color operators
        // are preservable here; operators requiring other default resources fail closed.
        var lexer = new PdfLexer(PdfCursor.FromBytes(text.Raw), 0, PdfLexer.MaxTokenBytes, false, CancellationToken.None);
        using var output = new MemoryStream();
        var serializer = new PdfSerializer(output, static (_, _) => throw new InvalidOperationException(), null);
        while (true)
        {
            PdfToken token = PdfSync.Result(lexer.NextAsync());
            if (token.Kind == PdfTokenKind.End)
            {
                break;
            }

            if (token.Kind == PdfTokenKind.Name && _fonts.TryGetValue(token.Text, out string? font))
            {
                serializer.WriteValue(new PdfObject.Name(font));
            }
            else if (token.Kind is PdfTokenKind.Integer or PdfTokenKind.Real)
            {
                serializer.WriteValue(token.Kind == PdfTokenKind.Integer ? PdfObject.Num.FromLong(token.Integer) : PdfObject.Num.FromDouble(token.Real));
            }
            else if (token.Kind == PdfTokenKind.Keyword && token.Text is "Tf" or "g" or "rg" or "k" or "Tc" or "Tw" or "Tz" or "TL" or "Tr" or "Ts")
            {
                byte[] bytes = Encoding.ASCII.GetBytes(token.Text);
                output.Write(bytes, 0, bytes.Length);
            }
            else
            {
                throw Unsupported("a default appearance or its font resources");
            }

            output.WriteByte(32);
            if (output.Length > PdfLexer.MaxTokenBytes)
            {
                throw new PdfResourceLimitException("default appearance bytes");
            }
        }

        _budget.AddStructuralBytes(output.Length);
        return new PdfObject.Str(output.ToArray(), false);
    }

    private PdfObject.Ref? Write(PdfAssemblyField node, PdfObject.Ref parent)
    {
        if (node.Widgets.Count == 0 && !HasWidgets(node.Children))
        {
            return null;
        }

        var field = new PdfObject.Dict();
        foreach (var entry in node.Source.Items)
        {
            if (FieldKeys.Contains(entry.Key))
            {
                field.Set(entry.Key, entry.Key == "DA" ? Appearance(entry.Value) : _copier.CopyDirect(_store, entry.Value, "resource"));
            }
        }

        field.Set("Parent", parent);
        PdfObject.Ref reference = _writer.Register(field);
        var kids = new PdfObject.Arr();
        foreach (PdfAssemblyField child in node.Children)
        {
            PdfObject.Ref? nested = Write(child, reference);
            if (nested != null)
            {
                kids.Items.Add(nested);
            }
        }

        for (int i = 0; i < node.Widgets.Count; i++)
        {
            node.Widgets[i].Set("Parent", reference);
            kids.Items.Add(node.WidgetReferences[i]);
        }

        if (kids.Items.Count > 0)
        {
            field.Set("Kids", kids);
        }

        return reference;
    }

    private PdfObject.Dict Dictionary(PdfObject value)
    {
        return _store.Resolve(value) as PdfObject.Dict ?? throw Unsupported("an unresolved field dictionary");
    }

    private PdfObject.Arr Array(PdfObject value)
    {
        return _store.Resolve(value) as PdfObject.Arr ?? throw Unsupported("an unresolved field array");
    }
}
