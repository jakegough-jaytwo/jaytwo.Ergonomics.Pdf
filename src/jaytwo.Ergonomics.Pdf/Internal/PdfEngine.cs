#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using jaytwo.Ergonomics.Pdf.AcroForm;
using jaytwo.Ergonomics.Pdf.Attachments;
using jaytwo.Ergonomics.Pdf.Metadata;
using jaytwo.Ergonomics.Pdf.Signatures;

namespace jaytwo.Ergonomics.Pdf.Internal;

internal sealed class PdfEngine : IDisposable
{
    internal const long MaxClassicOffset = 9999999999L;

    private const int MaxTraversalDepth = 256;
    private const int FlagReadOnly = 1;
    private const int FlagRequired = 1 << 1;
    private const int FlagMultiline = 1 << 12;
    private const int FlagPassword = 1 << 13;
    private const int FlagRadio = 1 << 15;
    private const int FlagPushbutton = 1 << 16;
    private const int FlagEdit = 1 << 18;
    private const int FlagFileSelect = 1 << 20;
    private const int FlagMultiSelect = 1 << 21;
    private const int FlagComb = 1 << 24;
    private const int FlagRichText = 1 << 25;

    private readonly Dictionary<int, XrefEntry> _xref = new Dictionary<int, XrefEntry>();
    private readonly Dictionary<int, PdfObject> _cache = new Dictionary<int, PdfObject>();
    private readonly Dictionary<int, Dictionary<int, PdfObject>> _objectStreams = new Dictionary<int, Dictionary<int, PdfObject>>();
    private readonly HashSet<int> _dirty = new HashSet<int>();
    private readonly HashSet<long> _loadedXrefs = new HashSet<long>();
    private readonly HashSet<int> _loadingObjects = new HashSet<int>();
    private readonly List<Stream> _ownedStreams = new List<Stream>();
    private Stream _stream;
    private PdfCursor _cursor;
    private bool _leaveOpen;
    private bool _disposed;
    private long _length;
    private long _startXref;
    private int _size;
    private int _rootId;
    private int _rootGen;
    private int? _infoId;
    private int _infoGen;
    private PdfObject.Dict? _inlineInfo;
    private PdfObject.Arr? _id;
    private bool _modDateExplicit;
    private bool _touchingModDate;
    private bool _stickyNeedAppearances;
    private int _helvId;
    private Dictionary<int, PdfObject?>? _backup;
    private PdfSecurity? _security;
    private int _encryptId;
    private int _encryptGen;
    private int _objectStreamParseDepth;

    private PdfEngine(Stream stream, bool leaveOpen)
    {
        _stream = stream;
        _leaveOpen = leaveOpen;
        _cursor = new PdfCursor(stream);
        _length = stream.Length;
    }

    internal int PageCount
    {
        get
        {
            using var release = ReleaseAfter();
            return GetPages().Count;
        }
    }

    internal int RetainedObjectStreamCount => _objectStreams.Count;

    internal PdfPermissions Permissions
    {
        get
        {
            CheckDisposed();
            return _security == null ? PdfPermissions.AllowAll() : _security.Permissions;
        }
    }

    internal IReadOnlyList<PdfPage> ListPages()
    {
        CheckDisposed();
        using var release = ReleaseAfter();
        List<PageInfo> pages = GetPages();
        var result = new List<PdfPage>(pages.Count);
        for (int i = 0; i < pages.Count; i++)
        {
            result.Add(new PdfPage(i + 1, pages[i].MediaBox, pages[i].CropBox, pages[i].Rotate));
        }

        return result;
    }

    internal bool HasFields
    {
        get
        {
            using var release = ReleaseAfter();
            return CollectFields().Count > 0;
        }
    }

    internal bool NeedAppearances
    {
        get
        {
            using var release = ReleaseAfter();
            if (CatalogDict().Get("AcroForm") is not PdfObject acro)
            {
                return false;
            }

            return Resolve(acro) is PdfObject.Dict dict && dict.Get("NeedAppearances") is PdfObject.Bool flag && flag.Value;
        }
    }

    internal bool HasXmp
    {
        get
        {
            using var release = ReleaseAfter();
            return CatalogDict().Get("Metadata") != null;
        }
    }

    internal bool HasUsageRights
    {
        get
        {
            using var release = ReleaseAfter();
            return ResolveMaybe(CatalogDict().Get("Perms")) is PdfObject.Dict perms && perms.Get("UR3") != null;
        }
    }

    internal static PdfEngine Open(Stream stream, bool leaveOpen, string? password)
    {
        var engine = new PdfEngine(stream, leaveOpen);
        try
        {
            engine.Initialize(password);
            return engine;
        }
        catch
        {
            engine.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (Stream owned in _ownedStreams)
        {
            owned.Dispose();
        }

        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }

    internal string? GetInfoText(string key)
    {
        CheckDisposed();
        using var release = ReleaseAfter();
        PdfObject.Dict? info = ReadInfo();
        return info == null ? null : ValueText(info.Get(key));
    }

    internal IReadOnlyList<string> InfoKeys()
    {
        CheckDisposed();
        using var release = ReleaseAfter();
        PdfObject.Dict? info = ReadInfo();
        if (info == null)
        {
            return Array.Empty<string>();
        }

        var keys = new List<string>(info.Items.Count);
        foreach ((string key, _) in info.Items)
        {
            keys.Add(key);
        }

        return keys;
    }

    internal void SetInfo(string key, string? value)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (key.Length == 0)
        {
            throw new ArgumentException("The document information key is empty.", nameof(key));
        }

        using var release = ReleaseAfter();
        if (key == "CreationDate" || key == "ModDate")
        {
            SetInfoDate(key, value == null ? null : PdfTextCodec.ParseDate(value), key == "ModDate");
            return;
        }

        if (key == "Trapped")
        {
            SetTrapped(value);
            return;
        }

        SetInfoText(key, value);
    }

    internal void SetInfoText(string key, string? value)
    {
        CheckDisposed();
        using var release = ReleaseAfter();
        EnsureDocumentChange();
        RequireModify();
        ApplyUsageRights(null, null);
        PdfObject.Dict info = EditInfo();
        if (value == null)
        {
            info.Remove(key);
        }
        else
        {
            info.Set(key, PdfTextCodec.Encode(value));
        }

        SyncXmp(key, value);
        TouchModDate();
    }

    private void SetTrapped(string? value)
    {
        CheckDisposed();
        if (value != null && value != "True" && value != "False" && value != "Unknown")
        {
            throw new ArgumentException("Trapped must be True, False, or Unknown.", nameof(value));
        }

        EnsureDocumentChange();
        RequireModify();
        ApplyUsageRights(null, null);
        PdfObject.Dict info = EditInfo();
        if (value == null)
        {
            info.Remove("Trapped");
        }
        else
        {
            info.Set("Trapped", new PdfObject.Name(value));
        }

        TouchModDate();
    }

    internal DateTimeOffset? GetInfoDate(string key)
    {
        CheckDisposed();
        using var release = ReleaseAfter();
        string? text = GetInfoText(key);
        return text == null ? null : PdfTextCodec.ParseDate(text);
    }

    internal void SetInfoDate(string key, DateTimeOffset? value, bool explicitModification)
    {
        CheckDisposed();
        using var release = ReleaseAfter();
        EnsureDocumentChange();
        RequireModify();
        ApplyUsageRights(null, null);
        if (explicitModification)
        {
            _modDateExplicit = true;
        }

        PdfObject.Dict info = EditInfo();
        string? xmp = null;
        if (value == null)
        {
            info.Remove(key);
        }
        else
        {
            info.Set(key, PdfTextCodec.Encode(PdfTextCodec.FormatDate(value.Value)));
            xmp = PdfTextCodec.FormatXmpDate(value.Value);
        }

        SyncXmp(key, xmp);
        if (!explicitModification)
        {
            TouchModDate();
        }
    }

    internal Stream OpenXmp()
    {
        CheckDisposed();
        using var release = ReleaseAfter();
        byte[]? bytes = TryReadXmpBytes() ?? throw new PdfXmpException("The document has no XMP metadata.");
        if (!XmpEditor.TryParse(bytes, out _, out Exception? error))
        {
            throw new PdfXmpException("XMP is not well-formed XML.", error);
        }

        return new MemoryStream(bytes, false);
    }

    internal void SetXmp(ReadOnlySpan<byte> utf8Packet)
    {
        CheckDisposed();
        using var release = ReleaseAfter();
        EnsureDocumentChange();
        RequireModify();
        ApplyUsageRights(null, null);
        var copy = new byte[utf8Packet.Length];
        utf8Packet.CopyTo(copy);
        WriteXmp(copy);
    }

    internal IReadOnlyList<PdfField> ListFields()
    {
        CheckDisposed();
        using var release = ReleaseAfter();
        var fields = new List<PdfField>();
        foreach (FieldSlot slot in CollectFields())
        {
            fields.Add(new PdfField(
                slot.PartialName,
                slot.FullName,
                slot.WidgetIndex,
                slot.FieldType,
                slot.ButtonKind,
                slot.SelectedValues,
                slot.ExportValue,
                slot.Rect,
                slot.PageNumber,
                slot.PageRotation,
                slot.MediaBox,
                slot.ReadOnly,
                slot.Required,
                slot.Multiline,
                slot.MaxLength,
                slot.Editable,
                slot.MultiSelect,
                slot.Options));
        }

        return fields;
    }

    internal IReadOnlyList<PdfField> GetFields(string fullName)
    {
        CheckDisposed();
        if (fullName == null)
        {
            throw new ArgumentNullException(nameof(fullName));
        }

        using var release = ReleaseAfter();
        var matches = new List<PdfField>();
        foreach (PdfField field in ListFields())
        {
            if (field.FullName == fullName)
            {
                matches.Add(field);
            }
        }

        if (matches.Count == 0)
        {
            throw new PdfFieldNotFoundException(fullName);
        }

        return matches;
    }

    internal PdfAppearanceDisposition SetText(string fullName, string value)
    {
        CheckDisposed();
        if (fullName == null)
        {
            throw new ArgumentNullException(nameof(fullName));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        using var release = ReleaseAfter();
        EnsureFieldChange(fullName);
        RequireFill();
        List<FieldSlot> matches = RequireFields(fullName);
        FieldSlot first = matches[0];
        if (first.ReadOnly)
        {
            throw new PdfFieldReadOnlyException(fullName);
        }

        if (first.FieldType != PdfFieldType.Text || first.Password)
        {
            throw new PdfFieldValueException("The field cannot be filled as text.");
        }

        if (first.MaxLength is int max && value.Length > max)
        {
            throw new PdfFieldValueException("The value exceeds /MaxLen.");
        }

        ApplyUsageRights("Form", "FillIn");
        bool simple = first.FieldType == PdfFieldType.Text
            && !first.Comb
            && !first.RichText
            && !first.FileSelect
            && PdfTextCodec.CanEncodeWinAnsi(value)
            && Math.Abs(first.Rect.Width) > 0
            && Math.Abs(first.Rect.Height) > 0;
        foreach (FieldSlot slot in matches)
        {
            PdfObject.Dict owner = (PdfObject.Dict)EditAny(slot.ValueObjectId);
            owner.Set("V", PdfTextCodec.Encode(value));
            PdfObject.Dict widget = slot.WidgetObjectId == slot.ValueObjectId
                ? owner
                : (PdfObject.Dict)EditAny(slot.WidgetObjectId);
            if (!simple)
            {
                widget.Remove("AP");
            }
        }

        if (!simple)
        {
            SetNeedAppearances(true);
            return PdfAppearanceDisposition.NeedAppearances;
        }

        int fontId = EnsureHelv();
        foreach (FieldSlot slot in matches)
        {
            var appearance = AppearanceBuilder.ParseDefaultAppearance(slot.DefaultAppearance);
            byte[] bytes = AppearanceBuilder.Build(
                value,
                Math.Abs(slot.Rect.Width),
                Math.Abs(slot.Rect.Height),
                appearance.Size,
                slot.Align,
                appearance.Red,
                appearance.Green,
                appearance.Blue,
                slot.Multiline);
            var stream = new PdfObject.Dict();
            stream.Set("Type", new PdfObject.Name("XObject"));
            stream.Set("Subtype", new PdfObject.Name("Form"));
            stream.Set("FormType", PdfObject.Num.FromLong(1));
            stream.Set("BBox", RectArray(new PdfRect(0, 0, Math.Abs(slot.Rect.Width), Math.Abs(slot.Rect.Height))));
            var resources = new PdfObject.Dict();
            var fonts = new PdfObject.Dict();
            fonts.Set("Helv", new PdfObject.Ref(fontId, 0));
            resources.Set("Font", fonts);
            stream.Set("Resources", resources);
            stream.Set("Length", PdfObject.Num.FromLong(bytes.Length));
            stream.Body = new PdfObject.MemoryBody(bytes);
            int appearanceId = Allocate(stream);
            PdfObject.Dict widget = (PdfObject.Dict)EditAny(slot.WidgetObjectId);
            var normal = new PdfObject.Dict();
            normal.Set("N", new PdfObject.Ref(appearanceId, 0));
            widget.Set("AP", normal);
        }

        if (!_stickyNeedAppearances)
        {
            SetNeedAppearances(false);
        }

        return PdfAppearanceDisposition.Generated;
    }

    internal PdfAppearanceDisposition SetCheckBox(string fullName, bool isChecked)
    {
        CheckDisposed();
        if (fullName == null)
        {
            throw new ArgumentNullException(nameof(fullName));
        }

        using var release = ReleaseAfter();
        EnsureFieldChange(fullName);
        RequireFill();
        List<FieldSlot> matches = RequireFields(fullName);
        if (matches[0].ReadOnly)
        {
            throw new PdfFieldReadOnlyException(fullName);
        }

        if (matches[0].ButtonKind != PdfButtonKind.CheckBox)
        {
            throw new PdfFieldValueException("The field is not a check box.");
        }

        string? on = null;
        foreach (FieldSlot slot in matches)
        {
            if (Load(slot.WidgetObjectId) is PdfObject.Dict widget)
            {
                string? widgetOn = OnState(widget);
                if (isChecked && on != null && widgetOn != null && widgetOn != on)
                {
                    throw new PdfFieldValueException("The check box widgets have different on-state names.");
                }

                on ??= widgetOn;
            }
        }

        ApplyUsageRights("Form", "FillIn");
        bool generated = false;
        if (on == null)
        {
            if (!TryWriteCheckBoxAppearances(matches))
            {
                SetNameValue(matches, isChecked ? "Yes" : "Off", setAppearanceState: false);
                SetNeedAppearances(true);
                return PdfAppearanceDisposition.NeedAppearances;
            }

            on = "Yes";
            generated = true;
        }

        SetNameValue(matches, isChecked ? on : "Off", setAppearanceState: true);
        if (generated && !_stickyNeedAppearances)
        {
            SetNeedAppearances(false);
        }

        return generated ? PdfAppearanceDisposition.Generated : PdfAppearanceDisposition.ExistingAppearanceSelected;
    }

    internal PdfAppearanceDisposition SetRadio(string fullName, string exportValue)
    {
        CheckDisposed();
        if (fullName == null)
        {
            throw new ArgumentNullException(nameof(fullName));
        }

        if (exportValue == null)
        {
            throw new ArgumentNullException(nameof(exportValue));
        }

        using var release = ReleaseAfter();
        EnsureFieldChange(fullName);
        RequireFill();
        List<FieldSlot> matches = RequireFields(fullName);
        if (matches[0].ReadOnly)
        {
            throw new PdfFieldReadOnlyException(fullName);
        }

        if (matches[0].ButtonKind != PdfButtonKind.Radio)
        {
            throw new PdfFieldValueException("The field is not a radio button.");
        }

        bool any = false;
        bool found = false;
        foreach (FieldSlot slot in matches)
        {
            if (slot.ExportValue != null)
            {
                any = true;
                if (slot.ExportValue == exportValue)
                {
                    found = true;
                }
            }
        }

        if (any && !found)
        {
            throw new PdfFieldValueException("The radio export value was not found.");
        }

        ApplyUsageRights("Form", "FillIn");
        if (!any)
        {
            if (matches.Count == 1 && TryWriteRadioAppearances(matches, exportValue))
            {
                SetNameValue(matches, exportValue, setAppearanceState: true);
                if (!_stickyNeedAppearances)
                {
                    SetNeedAppearances(false);
                }

                return PdfAppearanceDisposition.Generated;
            }

            SetNameValue(matches, exportValue, setAppearanceState: false);
            SetNeedAppearances(true);
            return PdfAppearanceDisposition.NeedAppearances;
        }

        foreach (FieldSlot slot in matches)
        {
            PdfObject.Dict widget = (PdfObject.Dict)EditAny(slot.WidgetObjectId);
            widget.Set("AS", new PdfObject.Name(slot.ExportValue == exportValue ? exportValue : "Off"));
        }

        PdfObject.Dict owner = (PdfObject.Dict)EditAny(matches[0].ValueObjectId);
        owner.Set("V", new PdfObject.Name(exportValue));
        return PdfAppearanceDisposition.ExistingAppearanceSelected;
    }

    internal PdfAppearanceDisposition SetChoice(string fullName, IReadOnlyList<string> values)
    {
        CheckDisposed();
        if (fullName == null)
        {
            throw new ArgumentNullException(nameof(fullName));
        }

        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (values.Count == 0)
        {
            throw new ArgumentException("A choice field requires a value.", nameof(values));
        }

        foreach (string item in values)
        {
            if (item == null)
            {
                throw new ArgumentException("Choice values cannot contain null.", nameof(values));
            }
        }

        using var release = ReleaseAfter();
        EnsureFieldChange(fullName);
        RequireFill();
        List<FieldSlot> matches = RequireFields(fullName);
        FieldSlot first = matches[0];
        if (first.ReadOnly)
        {
            throw new PdfFieldReadOnlyException(fullName);
        }

        if (first.FieldType != PdfFieldType.Choice)
        {
            throw new PdfFieldValueException("The field is not a choice field.");
        }

        if (!first.MultiSelect && values.Count != 1)
        {
            throw new PdfFieldValueException("The choice field accepts one value.");
        }

        foreach (string item in values)
        {
            RequireOption(first, item);
        }

        ApplyUsageRights("Form", "FillIn");
        foreach (FieldSlot slot in matches)
        {
            PdfObject.Dict owner = (PdfObject.Dict)EditAny(slot.ValueObjectId);
            owner.Remove("I");
            if (first.MultiSelect)
            {
                var array = new PdfObject.Arr();
                foreach (string item in values)
                {
                    array.Items.Add(PdfTextCodec.Encode(item));
                }

                owner.Set("V", array);
            }
            else
            {
                owner.Set("V", PdfTextCodec.Encode(values[0]));
            }

            PdfObject.Dict widget = (PdfObject.Dict)EditAny(slot.WidgetObjectId);
            widget.Remove("AP");
        }

        if (TryWriteChoiceAppearance(matches, values))
        {
            return PdfAppearanceDisposition.Generated;
        }

        SetNeedAppearances(true);
        return PdfAppearanceDisposition.NeedAppearances;
    }

    private static void RequireOption(FieldSlot slot, string value)
    {
        if (slot.Editable)
        {
            return;
        }

        foreach (PdfChoiceOption option in slot.Options)
        {
            if (option.Value == value)
            {
                return;
            }
        }

        throw new PdfFieldValueException("The choice value is not in /Opt.");
    }

    internal IReadOnlyList<PdfAttachment> ListAttachments()
    {
        CheckDisposed();
        using var release = ReleaseAfter();
        var list = new List<PdfAttachment>();
        foreach (AttachmentSlot slot in ReadAttachments())
        {
            list.Add(new PdfAttachment(this, slot.Name, slot.Description, slot.MimeType, slot.Size));
        }

        return list;
    }

    internal PdfAttachment AddAttachment(string name, Stream content, bool leaveOpen, PdfAttachmentOptions? options)
    {
        CheckDisposed();
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (!content.CanSeek)
        {
            throw new ArgumentException("Attachment content must be a seekable stream.", nameof(content));
        }

        using var release = ReleaseAfter();
        EnsureDocumentChange();
        RequireModify();
        List<AttachmentSlot> slots = ReadAttachments();
        int replaced = slots.RemoveAll(slot => slot.Name == name);
        ApplyUsageRights("EF", replaced == 0 ? "Create" : "Modify");
        long start = content.Position;
        long length = content.Length - start;
        var embedded = new PdfObject.Dict();
        embedded.Set("Type", new PdfObject.Name("EmbeddedFile"));
        if (options?.MimeType is string mimeType && mimeType.Length > 0)
        {
            embedded.Set("Subtype", new PdfObject.Name(mimeType));
        }

        var parameters = new PdfObject.Dict();
        parameters.Set("Size", PdfObject.Num.FromLong(length));
        embedded.Set("Params", parameters);
        embedded.Set("Length", PdfObject.Num.FromLong(length));
        embedded.Body = new PdfObject.ExternalBody(content, start, length);
        int streamId = Allocate(embedded);
        var spec = new PdfObject.Dict();
        spec.Set("Type", new PdfObject.Name("Filespec"));
        spec.Set("F", PdfTextCodec.Encode(AsciiFallback(name)));
        spec.Set("UF", PdfTextCodec.Encode(name));
        var ef = new PdfObject.Dict();
        ef.Set("F", new PdfObject.Ref(streamId, 0));
        ef.Set("UF", new PdfObject.Ref(streamId, 0));
        spec.Set("EF", ef);
        spec.Set("AFRelationship", new PdfObject.Name("Unspecified"));
        if (options?.Description is string description && description.Length > 0)
        {
            spec.Set("Desc", PdfTextCodec.Encode(description));
        }

        int specId = Allocate(spec);
        if (!leaveOpen)
        {
            _ownedStreams.Add(content);
        }

        slots.Add(new AttachmentSlot(name, PdfTextCodec.Encode(name).Raw, specId, options?.Description, options?.MimeType, length, streamId));
        WriteAttachments(slots);
        return new PdfAttachment(this, name, options?.Description, options?.MimeType, length);
    }

    internal void RemoveAttachment(string name)
    {
        CheckDisposed();
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        using var release = ReleaseAfter();
        EnsureDocumentChange();
        RequireModify();
        List<AttachmentSlot> slots = ReadAttachments();
        if (slots.RemoveAll(slot => slot.Name == name) == 0)
        {
            throw new PdfAttachmentNotFoundException(name);
        }

        ApplyUsageRights("EF", "Delete");
        WriteAttachments(slots);
    }

    internal Stream OpenAttachment(string name)
    {
        CheckDisposed();
        using var release = ReleaseAfter();
        AttachmentSlot slot = ReadAttachments().FirstOrDefault(item => item.Name == name) ?? throw new PdfAttachmentNotFoundException(name);
        if (slot.StreamId == 0 || Load(slot.StreamId) is not PdfObject.Dict dict)
        {
            throw new PdfParseException(0, "Embedded file is not a stream.");
        }

        return OpenBody(dict);
    }

    internal void SaveIncremental()
    {
        using var release = ReleaseAfter();
        SaveIncremental(_stream, _leaveOpen);
    }

    internal void SaveIncremental(Stream destination, bool leaveOpen)
    {
        CheckDisposed();
        using var release = ReleaseAfter();
        ValidateDestination(destination);
        if (_length > MaxClassicOffset)
        {
            throw new PdfTooLargeForClassicXrefException();
        }

        bool same = ReferenceEquals(destination, _stream);
        if (_dirty.Count == 0)
        {
            if (!same)
            {
                CopyPrefix(destination);
                SwitchStream(destination, leaveOpen);
            }

            return;
        }

        EnsureAppendAllowed();
        if (!same)
        {
            CopyPrefix(destination);
        }
        else
        {
            destination.Seek(_length, SeekOrigin.Begin);
        }

        if (NeedsLeadingLineFeed())
        {
            destination.WriteByte((byte)'\n');
        }

        AppendixLayout layout = WriteAppendix(destination, 0);
        FinishWrite(destination, leaveOpen, same, layout);
    }

    internal void Sign(PdfSignatureOptions options, IPdfSignatureProvider provider)
    {
        using var release = ReleaseAfter();
        Sign(_stream, options, provider, _leaveOpen);
    }

    internal void Sign(Stream destination, PdfSignatureOptions options, IPdfSignatureProvider provider, bool leaveOpen)
    {
        CheckDisposed();
        using var release = ReleaseAfter();
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (provider == null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        if (provider.Kind != options.Kind)
        {
            throw new PdfSignException("The signature provider does not match the requested signature kind.");
        }

        ValidateDestination(destination);
        if (_length > MaxClassicOffset)
        {
            throw new PdfTooLargeForClassicXrefException();
        }

        if (options.ReservedContentsBytes < 1 || options.ReservedContentsBytes > (1024 * 1024))
        {
            throw new PdfSignException("Reserved contents size must be between 1 and 1048576 bytes.");
        }

        if (NeedAppearances)
        {
            throw new PdfSignException("Cannot sign while /NeedAppearances is true.");
        }

        EnsureSigningAllowed();
        RequireAnnotate();

        // A strict lower bound on the appendix, so it never refuses one that fits. The /Contents
        // gap and every dirty stream body are written in full: plain, file-backed, and RC4 bodies
        // keep their length, and AES adds an IV and padding. PdfAppendixBuffer is the exact check.
        long estimate = 2L + (options.ReservedContentsBytes * 2L);
        foreach (int id in _dirty)
        {
            if (_cache.TryGetValue(id, out PdfObject? obj) && obj is PdfObject.Dict dict && dict.Body != null)
            {
                estimate += dict.Body.Length;
            }
        }

        if (estimate > PdfAppendixBuffer.MaxLength)
        {
            throw new PdfSignException(PdfAppendixBuffer.TooLargeMessage);
        }

        int savedSize = _size;
        int? savedInfo = _infoId;
        int savedInfoGen = _infoGen;
        PdfObject.Arr? savedId = _id;
        int savedHelv = _helvId;
        bool savedSticky = _stickyNeedAppearances;
        var savedDirty = new HashSet<int>(_dirty);
        _backup = new Dictionary<int, PdfObject?>();
        try
        {
            if (_id == null)
            {
                _id = NewId();
            }

            ApplyUsageRights(null, null);
            DateTimeOffset? signingTime = options.Kind == PdfSignatureKind.DocTimeStamp
                ? null
                : options.SigningTime ?? DateTimeOffset.UtcNow;
            BuildSignature(options, signingTime);

            // The estimate above runs before BuildSignature dirties a reused field. The bounded
            // buffer is the exact guard: it throws before the destination is touched.
            using var buffer = new PdfAppendixBuffer();
            if (NeedsLeadingLineFeed())
            {
                buffer.WriteByte((byte)'\n');
            }

            AppendixLayout layout = WriteAppendix(buffer, _length);
            if (layout.Contents == null || layout.ByteRange == null)
            {
                throw new PdfSignException("The signature dictionary did not reserve /ByteRange and /Contents.");
            }

            long gap = 2L + (options.ReservedContentsBytes * 2L);
            long less = _length + layout.Contents.LessThanPosition;
            long total = _length + buffer.Length;
            var range = new PdfByteRange(0, less, less + gap, total - less - gap);
            PatchAscii(buffer, layout.ByteRange.DigitPosition, Ten(range.Offset0) + " " + Ten(range.Length0) + " " + Ten(range.Offset1) + " " + Ten(range.Length1));
            var context = new PdfSignatureContext(range, options.ReservedContentsBytes, signingTime, () => OpenSigned(buffer, layout.Contents.LessThanPosition, gap));
            byte[] contents = provider.CreateContents(context);
            if (contents.Length > options.ReservedContentsBytes)
            {
                throw new PdfSignatureTooLargeException(contents.Length, options.ReservedContentsBytes);
            }

            var hex = new char[options.ReservedContentsBytes * 2];
            for (int i = 0; i < hex.Length; i++)
            {
                hex[i] = '0';
            }

            string encoded = PdfTextCodec.ToUpperHex(contents);
            encoded.CopyTo(0, hex, 0, encoded.Length);
            PatchAscii(buffer, layout.Contents.HexPosition, new string(hex));
            bool same = ReferenceEquals(destination, _stream);
            if (!same)
            {
                CopyPrefix(destination);
            }
            else
            {
                destination.Seek(_length, SeekOrigin.Begin);
            }

            buffer.WriteTo(destination);
            FinishWrite(destination, leaveOpen, same, layout);
            _backup = null;
        }
        catch
        {
            Restore(savedSize, savedInfo, savedInfoGen, savedId, savedHelv, savedSticky, savedDirty);
            throw;
        }
    }

    private void Initialize(string? password)
    {
        if (_length < 8)
        {
            throw new PdfParseException(0, "The stream is too small to be a PDF.");
        }

        EnsureHeader();
        _startXref = FindStartXref();
        LoadXref(_startXref, true, true, new HashSet<long>(), password);
        if (_rootId == 0)
        {
            throw new PdfParseException(_startXref, "The trailer has no /Root.");
        }

        foreach (int key in _xref.Keys)
        {
            if (key >= _size && key < int.MaxValue)
            {
                _size = key + 1;
            }
        }
    }

    private void EnsureHeader()
    {
        int count = (int)Math.Min(1024, _length);
        byte[] head = ReadRange(0, count);
        for (int i = 0; i < head.Length - 7; i++)
        {
            if (head[i] == (byte)'%'
                && head[i + 1] == (byte)'P'
                && head[i + 2] == (byte)'D'
                && head[i + 3] == (byte)'F'
                && head[i + 4] == (byte)'-'
                && (head[i + 5] == (byte)'1' || head[i + 5] == (byte)'2')
                && head[i + 6] == (byte)'.')
            {
                return;
            }
        }

        throw new PdfParseException(0, "A %PDF-1. or %PDF-2. header was not found in the first 1024 bytes.");
    }

    private long FindStartXref()
    {
        foreach (long window in new long[] { 65536, 1024 * 1024 })
        {
            long size = Math.Min(window, _length);
            long start = _length - size;
            byte[] tail = ReadRange(start, (int)size);
            byte[] needle = Encoding.ASCII.GetBytes("startxref");
            int index = tail.Length;
            while (true)
            {
                index = LastIndexOf(tail, needle, index - 1);
                if (index < 0)
                {
                    break;
                }

                if (!TryParseStartXref(tail, index + needle.Length, out long value))
                {
                    continue;
                }

                if (value < 0 || value >= _length)
                {
                    throw new PdfParseException(start + index, "startxref points past EOF.");
                }

                return value;
            }
        }

        throw new PdfParseException(_length, "startxref was not found.");
    }

    private void LoadXref(long offset, bool overwrite, bool recordTrailer, HashSet<long> stack, string? password)
    {
        if (!stack.Add(offset))
        {
            throw new PdfParseException(offset, "Cyclic /Prev chain.");
        }

        if (stack.Count > MaxTraversalDepth)
        {
            throw new PdfParseException(offset, "The cross-reference chain is too deep.");
        }

        if (_loadedXrefs.Contains(offset))
        {
            stack.Remove(offset);
            return;
        }

        bool classic = IsClassicXref(offset);
        _cursor.Seek(offset);
        var parser = new PdfParser(_cursor, Resolve, 0);
        int budget = PdfParser.XrefEntryBudget(_length);
        PdfObject.Dict trailer;
        bool pendingEncrypt;
        if (classic)
        {
            // The table is merged as it is read, before its /XRefStm stream and before /Prev, the search order of
            // ISO 32000-1 7.5.8.4. A /Prev chain never holds a second copy of the map.
            trailer = parser.ParseClassicXref(_xref, overwrite, budget);
            pendingEncrypt = !TryEnsureSecurity(trailer, _xref, password);
        }
        else
        {
            PdfObject obj = parser.ParseIndirect(-1);
            if (obj is not PdfObject.Dict streamDict || streamDict.Body == null)
            {
                throw new PdfParseException(offset, "Xref stream object was expected.");
            }

            // A stream section is merged as it is read, so a /Prev chain never holds a second copy of the map.
            ReadXrefStream(parser, streamDict, overwrite, budget);
            pendingEncrypt = !TryEnsureSecurity(streamDict, _xref, password);
            trailer = streamDict;
        }

        if (classic && LongOrNull(trailer.Get("XRefStm")) is long streamOffset)
        {
            LoadXref(streamOffset, false, false, stack, password);
        }

        PdfObject.Dict? encryptedInline = null;
        if (recordTrailer)
        {
            TakeTrailer(trailer);
            if (_inlineInfo != null && _security == null)
            {
                encryptedInline = _inlineInfo;
            }
        }

        _loadedXrefs.Add(offset);
        if (LongOrNull(trailer.Get("Prev")) is long previous)
        {
            LoadXref(previous, false, _rootId == 0, stack, password);
        }

        bool chainComplete = stack.Count == 1;
        if (pendingEncrypt && !TryEnsureSecurity(trailer, _xref, password) && chainComplete)
        {
            throw new PdfParseException(offset, "The encryption dictionary is not an uncompressed object in the cross-reference table.");
        }

        if (encryptedInline != null && _security != null)
        {
            _security.DecryptStrings(encryptedInline);
        }

        stack.Remove(offset);
    }

    private void TakeTrailer(PdfObject.Dict trailer)
    {
        if (_rootId == 0 && trailer.Get("Root") is PdfObject.Ref root)
        {
            _rootId = root.ObjectId;
            _rootGen = root.GenerationNumber;
        }

        if (_infoId == null && _inlineInfo == null)
        {
            if (trailer.Get("Info") is PdfObject.Ref info)
            {
                _infoId = info.ObjectId;
                _infoGen = info.GenerationNumber;
            }
            else if (trailer.Get("Info") is PdfObject.Dict inline)
            {
                _security?.DecryptStrings(inline);
                _inlineInfo = inline;
            }
        }

        if (_id == null && trailer.Get("ID") is PdfObject.Arr id)
        {
            _id = (PdfObject.Arr)id.DeepClone();
        }

        if (_size == 0 && LongOrNull(trailer.Get("Size")) is long size)
        {
            _size = size > int.MaxValue ? int.MaxValue : (int)size;
        }
    }

    private bool TryEnsureSecurity(PdfObject.Dict trailer, Dictionary<int, XrefEntry> entries, string? password)
    {
        if (_security != null || _encryptId != 0)
        {
            return true;
        }

        if (trailer.Get("Encrypt") is PdfObject.Dict)
        {
            throw new PdfEncryptedException("The encryption dictionary must be an indirect object.");
        }

        if (trailer.Get("Encrypt") is not PdfObject.Ref reference)
        {
            return true;
        }

        if (!TryEncryptOffset(reference, entries, out long encryptOffset))
        {
            return false;
        }

        _encryptId = reference.ObjectId;
        _encryptGen = reference.GenerationNumber;
        PdfObject.Dict encrypt = ParseEncryptDictionary(encryptOffset, reference.ObjectId);
        _cache[reference.ObjectId] = encrypt;
        if (!PdfSecurity.IsSupportedRevision(encrypt))
        {
            throw new PdfEncryptedException("This encryption is not supported.");
        }

        _security = PdfSecurity.Create(encrypt, FileId(trailer), password, _encryptId);
        return true;
    }

    private bool TryEncryptOffset(PdfObject.Ref reference, Dictionary<int, XrefEntry> entries, out long offset)
    {
        if (TryUncompressedOffset(entries, reference, out offset))
        {
            return true;
        }

        return TryUncompressedOffset(_xref, reference, out offset);
    }

    // The encryption dictionary must resolve. Reading it as the null object would treat ciphertext as plaintext.
    private static bool TryUncompressedOffset(Dictionary<int, XrefEntry> entries, PdfObject.Ref reference, out long offset)
    {
        if (!entries.TryGetValue(reference.ObjectId, out XrefEntry entry))
        {
            offset = 0;
            return false;
        }

        if (entry.Type != 1)
        {
            throw new PdfParseException(entry.Field1, "The encryption dictionary is not an uncompressed object.");
        }

        if (entry.Field2 != reference.GenerationNumber)
        {
            throw new PdfParseException(entry.Field1, "The encryption dictionary reference does not match its cross-reference generation.");
        }

        offset = entry.Field1;
        return true;
    }

    private PdfObject.Dict ParseEncryptDictionary(long offset, int id)
    {
        _cursor.Seek(offset);
        var parser = new PdfParser(_cursor, null, 0);
        if (parser.ParseIndirect(id) is not PdfObject.Dict dict)
        {
            throw new PdfParseException(offset, "The encryption dictionary is not a dictionary.");
        }

        return dict;
    }

    private void ReadXrefStream(PdfParser parser, PdfObject.Dict dict, bool overwrite, int budget)
    {
        if (dict.Body is not PdfObject.FileBody body)
        {
            throw new PdfParseException(0, "Xref stream has no data.");
        }

        if (body.Length > PdfStreamDecoder.XrefCap)
        {
            throw new PdfObjectTooLargeException("The xref stream is larger than " + PdfStreamDecoder.XrefCap.ToString(CultureInfo.InvariantCulture) + " bytes.");
        }

        byte[] raw = ReadRange(body.Offset, (int)body.Length);

        // ISO 32000-1 7.5.8.2: an xref stream's /Filter and /DecodeParms are direct, so no resolver is passed.
        List<string> filters = PdfStreamDecoder.FiltersOf(dict);
        List<PdfObject.Dict?> parms = PdfStreamDecoder.ParmsOf(dict, filters.Count);
        PdfParser.EnsureXrefColumns(dict, parms);
        byte[] decoded;
        try
        {
            decoded = PdfStreamDecoder.Decode(raw, filters, parms, PdfStreamDecoder.XrefCap);
        }
        catch (PdfParseException ex)
        {
            throw new PdfParseException(body.Offset, "The xref stream could not be decoded. " + ex.Message);
        }

        parser.ParseXrefEntries(dict, decoded, _xref, overwrite, budget);
    }

    private Stream OpenStored(PdfObject.Dict dict, PdfObject.FileBody file)
    {
        if (_security == null || !_security.EncryptsStream(dict))
        {
            return new PdfSliceStream(_stream, file.Offset, file.Length);
        }

        if (_security.UsesInitializationVector(dict))
        {
            if (file.Length < 32)
            {
                throw new PdfParseException(file.Offset, "Encrypted stream is truncated.");
            }

            byte[] initialization = ReadRange(file.Offset, 16);
            var rest = new PdfSliceStream(_stream, file.Offset + 16, file.Length - 16);
            return _security.OpenDecrypted(rest, initialization, dict);
        }

        return _security.OpenDecrypted(new PdfSliceStream(_stream, file.Offset, file.Length), dict);
    }

    private static byte[] FileId(PdfObject.Dict trailer)
    {
        if (trailer.Get("ID") is PdfObject.Arr id && id.Items.Count > 0 && id.Items[0] is PdfObject.Str text && text.Raw.Length > 0)
        {
            var copy = new byte[text.Raw.Length];
            Buffer.BlockCopy(text.Raw, 0, copy, 0, copy.Length);
            return copy;
        }

        throw new PdfParseException(0, "An encrypted PDF has no file identifier.");
    }

    private PdfObject Load(int id)
    {
        if (_cache.TryGetValue(id, out PdfObject? cached))
        {
            return cached;
        }

        if (!_xref.TryGetValue(id, out XrefEntry entry) || entry.Type == 0)
        {
            throw new PdfParseException(0, "Object " + id.ToString(CultureInfo.InvariantCulture) + " is not in use.");
        }

        if (_loadingObjects.Contains(id))
        {
            throw new PdfParseException(0, "Cyclic indirect object dependency.");
        }

        if (_loadingObjects.Count >= MaxTraversalDepth)
        {
            throw new PdfParseException(0, "Indirect object dependency is too deep.");
        }

        _loadingObjects.Add(id);
        try
        {
            PdfObject obj;
            if (entry.Type == 1)
            {
                _cursor.Seek(entry.Field1);
                var parser = new PdfParser(_cursor, Resolve, 0);
                obj = parser.ParseIndirect(id);
            }
            else if (entry.Type == 2)
            {
                obj = LoadFromObjectStream((int)entry.Field1, entry.Field2, id);
            }
            else
            {
                throw new PdfParseException(entry.Field1, "Unknown xref entry type.");
            }

            obj.ObjectNumber = id;
            if (entry.Type == 2)
            {
                obj.Generation = 0;
            }

            if (_security != null && entry.Type != 2)
            {
                _security.DecryptStrings(obj);
            }

            _cache[id] = obj;
            return obj;
        }
        finally
        {
            _loadingObjects.Remove(id);
        }
    }

    private PdfObject LoadFromObjectStream(int streamId, int index, int expectedId)
    {
        if (!_objectStreams.TryGetValue(streamId, out Dictionary<int, PdfObject>? map))
        {
            if (!_xref.TryGetValue(streamId, out XrefEntry entry) || entry.Type != 1)
            {
                throw new PdfParseException(0, "Object stream is not an uncompressed object.");
            }

            ReleaseCleanObjectStreams(expectedId, null);
            map = ParseObjectStream(streamId);
            _objectStreams[streamId] = map;
            ReleaseCleanObjectStreams(expectedId, streamId);
        }

        if (!map.TryGetValue(expectedId, out PdfObject? obj))
        {
            throw new PdfParseException(0, "Object " + expectedId.ToString(CultureInfo.InvariantCulture) + " was not found in object stream " + streamId.ToString(CultureInfo.InvariantCulture) + ".");
        }

        if (index < 0)
        {
            throw new PdfParseException(0, "Object stream index is negative.");
        }

        return obj.DeepClone();
    }

    private Dictionary<int, PdfObject> ParseObjectStream(int streamId)
    {
        _objectStreamParseDepth++;
        try
        {
            if (Load(streamId) is not PdfObject.Dict dict)
            {
                throw new PdfParseException(0, "Object stream is not a dictionary.");
            }

            int count = ObjectStreamInteger(dict, "N");
            int first = ObjectStreamInteger(dict, "First");
            if (count < 0 || count > int.MaxValue / 2)
            {
                throw new PdfParseException(0, "Object stream /N is outside the supported range.");
            }

            // Each header pair needs at least "0 0" plus a separator, so /N pairs need 4N - 1 bytes before /First.
            if (count > 0 && ((long)count * 4) - 1 > first)
            {
                throw new PdfParseException(0, "Object stream /N pairs do not fit before /First.");
            }

            byte[] data = ReadDecoded(dict, PdfStreamDecoder.StructuralCap);
            if (first < 0 || first > data.Length)
            {
                throw new PdfParseException(0, "Object stream /First is outside the stream.");
            }

            return PdfParser.ParseObjectStream(data, count, first);
        }
        finally
        {
            _objectStreamParseDepth--;
        }
    }

    private ObjectStreamLease ReleaseAfter()
    {
        return new ObjectStreamLease(this);
    }

    private void ReleaseCleanObjectStreams(int? loadingId, int? keepStreamId)
    {
        if (_objectStreamParseDepth > 0 && loadingId == null && keepStreamId == null)
        {
            return;
        }

        if (_objectStreams.Count == 0)
        {
            return;
        }

        List<int>? drop = null;
        foreach (KeyValuePair<int, Dictionary<int, PdfObject>> item in _objectStreams)
        {
            if (keepStreamId != null && item.Key == keepStreamId.Value)
            {
                continue;
            }

            if (ContainsDirtyObject(item.Value))
            {
                continue;
            }

            drop ??= new List<int>();
            drop.Add(item.Key);
        }

        if (drop == null)
        {
            return;
        }

        foreach (int streamId in drop)
        {
            Dictionary<int, PdfObject> map = _objectStreams[streamId];
            foreach (int id in map.Keys)
            {
                if (KeepCached(id, loadingId))
                {
                    continue;
                }

                _cache.Remove(id);
            }

            _objectStreams.Remove(streamId);
        }
    }

    private bool ContainsDirtyObject(Dictionary<int, PdfObject> map)
    {
        foreach (int id in map.Keys)
        {
            if (_dirty.Contains(id))
            {
                return true;
            }
        }

        return false;
    }

    private bool KeepCached(int id, int? loadingId)
    {
        if (_dirty.Contains(id) || id == _rootId || (_encryptId != 0 && id == _encryptId))
        {
            return true;
        }

        if (loadingId != null && id == loadingId.Value)
        {
            return true;
        }

        return _infoId != null && id == _infoId.Value;
    }

    private PdfObject Resolve(PdfObject value)
    {
        var seen = new HashSet<int>();
        while (value is PdfObject.Ref reference)
        {
            if (!seen.Add(reference.ObjectId))
            {
                throw new PdfParseException(0, "Cyclic indirect object reference.");
            }

            if (seen.Count > MaxTraversalDepth)
            {
                throw new PdfParseException(0, "Indirect object reference chain is too deep.");
            }

            if (!IsLive(reference))
            {
                return PdfObject.NullObj.Instance;
            }

            value = Load(reference.ObjectId);
        }

        return value;
    }

    // ISO 32000-1 7.3.10 and 7.5.8.3: a reference to an undefined or free object, to an entry of an unknown type, or
    // with a generation other than the entry's is a reference to the null object. An object-stream member has generation 0.
    private bool IsLive(PdfObject.Ref reference)
    {
        if (!_xref.TryGetValue(reference.ObjectId, out XrefEntry entry))
        {
            return false;
        }

        return entry.Type switch
        {
            1 => entry.Field2 == reference.GenerationNumber,
            2 => reference.GenerationNumber == 0,
            _ => false,
        };
    }

    private bool IsDangling(PdfObject? value)
    {
        return value is PdfObject.Ref reference && !IsLive(reference);
    }

    private PdfObject? ResolveMaybe(PdfObject? value)
    {
        return value == null ? null : Resolve(value);
    }

    private PdfObject EditAny(int id)
    {
        Backup(id);
        if (_dirty.Contains(id) && _cache.TryGetValue(id, out PdfObject? edited))
        {
            return edited;
        }

        PdfObject source = Load(id);
        PdfObject clone = source.DeepClone();
        clone.ObjectNumber = source.ObjectNumber;
        clone.Generation = source.Generation;
        _cache[id] = clone;
        _dirty.Add(id);
        return clone;
    }

    private int Allocate(PdfObject obj)
    {
        int id = _size++;
        Backup(id);
        obj.ObjectNumber = id;
        obj.Generation = 0;
        _cache[id] = obj;
        _dirty.Add(id);
        _xref[id] = new XrefEntry(1, 0, 0);
        return id;
    }

    private void Backup(int id)
    {
        if (_backup == null || _backup.ContainsKey(id))
        {
            return;
        }

        if (_cache.TryGetValue(id, out PdfObject? cached))
        {
            _backup[id] = cached.DeepClone();
            return;
        }

        if (_xref.ContainsKey(id))
        {
            _backup[id] = Load(id).DeepClone();
            return;
        }

        _backup[id] = null;
    }

    private void Restore(int size, int? infoId, int infoGen, PdfObject.Arr? id, int helv, bool sticky, HashSet<int> dirty)
    {
        if (_backup != null)
        {
            foreach (KeyValuePair<int, PdfObject?> item in _backup)
            {
                if (item.Value == null)
                {
                    _cache.Remove(item.Key);
                    _xref.Remove(item.Key);
                }
                else
                {
                    _cache[item.Key] = item.Value;
                }
            }
        }

        _backup = null;
        _size = size;
        _infoId = infoId;
        _infoGen = infoGen;
        _id = id;
        _helvId = helv;
        _stickyNeedAppearances = sticky;
        _dirty.Clear();
        foreach (int dirtyId in dirty)
        {
            _dirty.Add(dirtyId);
        }
    }

    private PdfObject.Dict CatalogDict()
    {
        if (Resolve(new PdfObject.Ref(_rootId, _rootGen)) is not PdfObject.Dict dict)
        {
            throw new PdfParseException(0, "The catalog is not a dictionary.");
        }

        return dict;
    }

    private PdfObject.Dict EditCatalog()
    {
        CatalogDict();
        return (PdfObject.Dict)EditAny(_rootId);
    }

    private PdfObject.Dict? ReadInfo()
    {
        if (_inlineInfo != null && _infoId == null)
        {
            return _inlineInfo;
        }

        if (_infoId == null)
        {
            return null;
        }

        return Resolve(new PdfObject.Ref(_infoId.Value, _infoGen)) as PdfObject.Dict;
    }

    private PdfObject.Dict EditInfo()
    {
        if (_infoId != null && (!IsLive(new PdfObject.Ref(_infoId.Value, _infoGen)) || ReadInfo() == null))
        {
            _infoId = null;
        }

        if (_infoId == null)
        {
            var created = _inlineInfo == null ? new PdfObject.Dict() : (PdfObject.Dict)_inlineInfo.DeepClone();
            _inlineInfo = null;
            _infoId = Allocate(created);
            _infoGen = 0;
            return created;
        }

        return (PdfObject.Dict)EditAny(_infoId.Value);
    }

    private void TouchModDate()
    {
        if (_modDateExplicit || _touchingModDate)
        {
            return;
        }

        _touchingModDate = true;
        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            PdfObject.Dict info = EditInfo();
            info.Set("ModDate", PdfTextCodec.Encode(PdfTextCodec.FormatDate(now)));
            SyncXmp("ModDate", PdfTextCodec.FormatXmpDate(now));
        }
        finally
        {
            _touchingModDate = false;
        }
    }

    private void SyncXmp(string key, string? value)
    {
        if (key != "Title" && key != "Author" && key != "Subject" && key != "Keywords" && key != "Creator" && key != "Producer" && key != "CreationDate" && key != "ModDate")
        {
            return;
        }

        byte[]? existing = TryReadXmpBytes();
        XDocument document;
        if (existing == null)
        {
            document = XmpEditor.CreateMinimal();
        }
        else if (!XmpEditor.TryParse(existing, out XDocument? parsed, out _))
        {
            return;
        }
        else
        {
            document = parsed!;
        }

        // A packet that parses but is not XMP, such as one without rdf:RDF, is malformed XMP too: leave its bytes unchanged.
        try
        {
            XmpEditor.ApplyInfo(document, key, value);
        }
        catch (PdfXmpException)
        {
            return;
        }

        WriteXmp(XmpEditor.ToBytes(document));
    }

    private byte[]? TryReadXmpBytes()
    {
        if (ResolveMaybe(CatalogDict().Get("Metadata")) is not PdfObject.Dict dict)
        {
            return null;
        }

        return ReadDecoded(dict, PdfStreamDecoder.StructuralCap);
    }

    private void WriteXmp(byte[] bytes)
    {
        PdfObject.Dict? metadata = ResolveMaybe(CatalogDict().Get("Metadata")) as PdfObject.Dict;
        PdfObject.Dict target;
        if (metadata == null || metadata.ObjectNumber == 0)
        {
            target = new PdfObject.Dict();
            target.Set("Type", new PdfObject.Name("Metadata"));
            target.Set("Subtype", new PdfObject.Name("XML"));
            int id = Allocate(target);
            EditCatalog().Set("Metadata", new PdfObject.Ref(id, 0));
        }
        else
        {
            target = (PdfObject.Dict)EditAny(metadata.ObjectNumber);
            target.Remove("Filter");
            target.Remove("DecodeParms");
        }

        target.Set("Type", new PdfObject.Name("Metadata"));
        target.Set("Subtype", new PdfObject.Name("XML"));
        target.Set("Length", PdfObject.Num.FromLong(bytes.Length));
        target.Body = new PdfObject.MemoryBody(bytes);
    }

    private List<PageInfo> GetPages()
    {
        var pages = new List<PageInfo>();
        PdfObject.Dict catalog = CatalogDict();
        if (ResolveMaybe(catalog.Get("Pages")) is not PdfObject.Dict root)
        {
            return pages;
        }

        int rootId = catalog.Get("Pages") is PdfObject.Ref reference ? reference.ObjectId : root.ObjectNumber;
        WalkPages(root, rootId, new PdfRect(0, 0, 0, 0), null, 0, pages, new HashSet<int>(), 0);
        return pages;
    }

    private void WalkPages(PdfObject.Dict node, int id, PdfRect media, PdfRect? crop, int rotate, List<PageInfo> pages, HashSet<int> seen, int depth)
    {
        if (!seen.Add(id))
        {
            throw new PdfParseException(0, "The page tree contains a cycle.");
        }

        if (depth > MaxTraversalDepth)
        {
            throw new PdfParseException(0, "The page tree is too deep.");
        }

        if (RectOf(node.Get("MediaBox")) is PdfRect box)
        {
            media = box;
        }

        if (RectOf(node.Get("CropBox")) is PdfRect cropBox)
        {
            crop = cropBox;
        }

        if (node.Get("Rotate") != null)
        {
            rotate = IntOf(node.Get("Rotate"));
        }

        string? type = NameOf(node.Get("Type"));
        PdfObject.Arr? kids = ResolveMaybe(node.Get("Kids")) as PdfObject.Arr;
        if (type == "Page" || (kids == null && type != "Pages"))
        {
            pages.Add(new PageInfo(id, media, crop ?? media, rotate));
            return;
        }

        if (kids == null)
        {
            return;
        }

        foreach (PdfObject kid in kids.Items)
        {
            if (kid is PdfObject.Ref reference && Resolve(reference) is PdfObject.Dict child)
            {
                WalkPages(child, reference.ObjectId, media, crop, rotate, pages, seen, depth + 1);
            }
        }
    }

    private List<FieldSlot> CollectFields()
    {
        var slots = new List<FieldSlot>();
        PdfObject.Dict catalog = CatalogDict();
        if (ResolveMaybe(catalog.Get("AcroForm")) is not PdfObject.Dict acro)
        {
            return slots;
        }

        if (ResolveMaybe(acro.Get("Fields")) is not PdfObject.Arr fields)
        {
            return slots;
        }

        List<PageInfo> pages = GetPages();
        var pageIndex = new Dictionary<int, int>();
        var annotPages = new Dictionary<int, int>();
        for (int i = 0; i < pages.Count; i++)
        {
            pageIndex[pages[i].ObjectId] = i + 1;
            if (Load(pages[i].ObjectId) is PdfObject.Dict page && ResolveMaybe(page.Get("Annots")) is PdfObject.Arr annots)
            {
                foreach (PdfObject annot in annots.Items)
                {
                    if (annot is PdfObject.Ref reference && IsLive(reference))
                    {
                        annotPages[reference.ObjectId] = i + 1;
                    }
                }
            }
        }

        var inherited = new Inherited(null, null, 0, null, 0, null, 0, null);
        var seenFields = new HashSet<int>();
        foreach (PdfObject field in fields.Items)
        {
            if (field is PdfObject.Ref reference && Resolve(reference) is PdfObject.Dict dict)
            {
                WalkField(dict, reference.ObjectId, string.Empty, inherited, slots, pages, pageIndex, annotPages, seenFields, 0);
            }
        }

        var counts = new Dictionary<string, int>();
        foreach (FieldSlot slot in slots)
        {
            counts.TryGetValue(slot.FullName, out int index);
            slot.WidgetIndex = index;
            counts[slot.FullName] = index + 1;
        }

        return slots;
    }

    private void WalkField(
        PdfObject.Dict field,
        int fieldId,
        string parentName,
        Inherited inherited,
        List<FieldSlot> slots,
        List<PageInfo> pages,
        Dictionary<int, int> pageIndex,
        Dictionary<int, int> annotPages,
        HashSet<int> seen,
        int depth)
    {
        if (!seen.Add(fieldId))
        {
            throw new PdfParseException(0, "The field tree contains a cycle.");
        }

        if (depth > MaxTraversalDepth)
        {
            throw new PdfParseException(0, "The field tree is too deep.");
        }

        string? ft = NameOf(field.Get("FT")) ?? inherited.Ft;
        PdfObject? value = field.Get("V") ?? inherited.Value;
        int flags = field.Get("Ff") != null ? IntOf(field.Get("Ff")) : inherited.Flags;
        string? da = ValueText(field.Get("DA")) ?? inherited.Da;
        int align = field.Get("Q") != null ? IntOf(field.Get("Q")) : inherited.Align;
        int? maxLen = field.Get("MaxLen") != null ? IntOf(field.Get("MaxLen")) : inherited.MaxLen;
        PdfObject? opt = field.Get("Opt") ?? inherited.Opt;
        int valueOwner = NameOf(field.Get("FT")) != null ? fieldId : inherited.ValueOwner;
        if (valueOwner == 0)
        {
            valueOwner = fieldId;
        }

        string? partial = ValueText(field.Get("T"));
        string full = string.IsNullOrEmpty(partial) ? parentName : (parentName.Length == 0 ? partial! : parentName + "." + partial);
        bool widget = NameOf(field.Get("Subtype")) == "Widget" || field.Get("Rect") != null;
        if (ResolveMaybe(field.Get("Kids")) is PdfObject.Arr kids && kids.Items.Count > 0)
        {
            var next = new Inherited(ft, value, flags, da, align, maxLen, valueOwner, opt);
            foreach (PdfObject kid in kids.Items)
            {
                if (kid is PdfObject.Ref reference && Resolve(reference) is PdfObject.Dict child)
                {
                    WalkField(child, reference.ObjectId, full, next, slots, pages, pageIndex, annotPages, seen, depth + 1);
                }
            }

            return;
        }

        if (!widget)
        {
            return;
        }

        int pageNumber = 0;
        if (field.Get("P") is PdfObject.Ref pageRef && IsLive(pageRef) && pageIndex.TryGetValue(pageRef.ObjectId, out int fromParent))
        {
            pageNumber = fromParent;
        }
        else if (annotPages.TryGetValue(fieldId, out int fromAnnots))
        {
            pageNumber = fromAnnots;
        }

        PageInfo page = pageNumber > 0 && pageNumber <= pages.Count ? pages[pageNumber - 1] : default;
        var type = ft switch
        {
            "Btn" => PdfFieldType.Button,
            "Ch" => PdfFieldType.Choice,
            "Sig" => PdfFieldType.Signature,
            _ => PdfFieldType.Text,
        };
        PdfButtonKind button = PdfButtonKind.None;
        if (type == PdfFieldType.Button)
        {
            if ((flags & FlagPushbutton) != 0)
            {
                button = PdfButtonKind.Push;
            }
            else if ((flags & FlagRadio) != 0)
            {
                button = PdfButtonKind.Radio;
            }
            else
            {
                button = PdfButtonKind.CheckBox;
            }
        }

        List<string> selected = ReadSelected(value);
        slots.Add(new FieldSlot
        {
            PartialName = partial ?? (full.Contains('.') ? full.Substring(full.LastIndexOf('.') + 1) : full),
            FullName = full,
            ValueObjectId = valueOwner,
            WidgetObjectId = fieldId,
            FieldType = type,
            ButtonKind = button,
            Value = selected.Count == 0 ? null : selected[0],
            SelectedValues = selected,
            ExportValue = type == PdfFieldType.Button ? OnState(field) : null,
            Rect = RectOf(field.Get("Rect")) ?? new PdfRect(0, 0, 0, 0),
            PageNumber = pageNumber,
            PageRotation = page.Rotate,
            MediaBox = page.MediaBox,
            ReadOnly = (flags & FlagReadOnly) != 0,
            Required = (flags & FlagRequired) != 0,
            Multiline = (flags & FlagMultiline) != 0,
            MaxLength = maxLen,
            Password = (flags & FlagPassword) != 0,
            FileSelect = (flags & FlagFileSelect) != 0,
            Comb = (flags & FlagComb) != 0,
            RichText = (flags & FlagRichText) != 0,
            Editable = type == PdfFieldType.Choice && (flags & FlagEdit) != 0,
            MultiSelect = type == PdfFieldType.Choice && (flags & FlagMultiSelect) != 0,
            Options = type == PdfFieldType.Choice ? ReadOptions(opt) : Array.Empty<PdfChoiceOption>(),
            DefaultAppearance = da,
            Align = align,
        });
    }

    private List<FieldSlot> RequireFields(string fullName)
    {
        List<FieldSlot> matches = CollectFields().Where(slot => slot.FullName == fullName).ToList();
        if (matches.Count == 0)
        {
            throw new PdfFieldNotFoundException(fullName);
        }

        return matches;
    }

    private void SetNameValue(List<FieldSlot> matches, string name, bool setAppearanceState)
    {
        PdfObject.Dict owner = (PdfObject.Dict)EditAny(matches[0].ValueObjectId);
        owner.Set("V", new PdfObject.Name(name));
        if (!setAppearanceState)
        {
            return;
        }

        foreach (FieldSlot slot in matches)
        {
            PdfObject.Dict widget = (PdfObject.Dict)EditAny(slot.WidgetObjectId);
            widget.Set("AS", new PdfObject.Name(name));
            if (slot.ValueObjectId != slot.WidgetObjectId)
            {
                continue;
            }
        }
    }

    private void SetNeedAppearances(bool value)
    {
        PdfObject.Dict acro = EnsureAcroForm();
        acro.Set("NeedAppearances", PdfObject.Bool.From(value));
        if (value)
        {
            _stickyNeedAppearances = true;
        }
    }

    private PdfObject.Dict EnsureAcroForm()
    {
        PdfObject.Dict catalog = EditCatalog();
        if (catalog.Get("AcroForm") is PdfObject.Ref reference && IsLive(reference))
        {
            return (PdfObject.Dict)EditAny(reference.ObjectId);
        }

        if (catalog.Get("AcroForm") is PdfObject.Dict inline)
        {
            return inline;
        }

        var created = new PdfObject.Dict();
        created.Set("Fields", new PdfObject.Arr());
        int id = Allocate(created);
        catalog.Set("AcroForm", new PdfObject.Ref(id, 0));
        return (PdfObject.Dict)_cache[id];
    }

    private int EnsureHelv()
    {
        if (_helvId != 0)
        {
            return _helvId;
        }

        PdfObject.Dict acro = EnsureAcroForm();
        if (ResolveMaybe(acro.Get("DR")) is PdfObject.Dict dr
            && ResolveMaybe(dr.Get("Font")) is PdfObject.Dict fonts
            && fonts.Get("Helv") is PdfObject.Ref existing
            && IsLive(existing))
        {
            _helvId = existing.ObjectId;
            return _helvId;
        }

        var font = new PdfObject.Dict();
        font.Set("Type", new PdfObject.Name("Font"));
        font.Set("Subtype", new PdfObject.Name("Type1"));
        font.Set("BaseFont", new PdfObject.Name("Helvetica"));
        font.Set("Encoding", new PdfObject.Name("WinAnsiEncoding"));
        _helvId = Allocate(font);
        PdfObject.Dict drEdit = acro.Get("DR") is PdfObject.Ref drRef && IsLive(drRef)
            ? (PdfObject.Dict)EditAny(drRef.ObjectId)
            : GetOrCreateDict(acro, "DR");
        PdfObject.Dict fontEdit = drEdit.Get("Font") is PdfObject.Ref fontRef && IsLive(fontRef)
            ? (PdfObject.Dict)EditAny(fontRef.ObjectId)
            : GetOrCreateDict(drEdit, "Font");
        fontEdit.Set("Helv", new PdfObject.Ref(_helvId, 0));
        return _helvId;
    }

    private static PdfObject.Dict GetOrCreateDict(PdfObject.Dict parent, string key)
    {
        if (parent.Get(key) is PdfObject.Dict dict)
        {
            return dict;
        }

        var created = new PdfObject.Dict();
        parent.Set(key, created);
        return created;
    }

    private void EnsureDocumentChange()
    {
        if (CertificationLevel() != 0)
        {
            throw new PdfCertificationLockedException();
        }
    }

    private void EnsureAppendAllowed()
    {
        if (CertificationLevel() == 1)
        {
            throw new PdfCertificationLockedException();
        }
    }

    private void EnsureSigningAllowed()
    {
        if (CertificationLevel() == 1)
        {
            throw new PdfCertificationLockedException();
        }
    }

    private void EnsureFieldChange(string fullName)
    {
        if (CertificationLevel() == 1 || FieldChangeBlocked(fullName))
        {
            throw new PdfCertificationLockedException();
        }
    }

    private void RequireModify()
    {
        if (_security != null && !_security.CanModify)
        {
            throw new PdfEncryptionRestrictedException();
        }
    }

    private void RequireFill()
    {
        if (_security != null && !_security.CanFill)
        {
            throw new PdfEncryptionRestrictedException();
        }
    }

    private void RequireAnnotate()
    {
        if (_security != null && !_security.CanAnnotate)
        {
            throw new PdfEncryptionRestrictedException();
        }
    }

    // Spec 7.3: the catalog /Perms /UR3 usage-rights signature is kept only when its UR3 transform parameters
    // (ISO 32000-1 12.8.2.3, Table 255) grant the write: `right` is listed in the `category` array. A write that no
    // right grants (a null category), and a /UR3 whose transform is absent, does not resolve, is malformed, or is
    // unknown, removes /UR3 from /Perms in the same revision. Callers run this after every check that can refuse
    // the write, so a refused write still does not dirty the document.
    private void ApplyUsageRights(string? category, string? right)
    {
        PdfObject? permsValue = CatalogDict().Get("Perms");
        if (ResolveMaybe(permsValue) is not PdfObject.Dict perms || perms.Get("UR3") is not PdfObject usageRights)
        {
            return;
        }

        if (category != null && right != null && UsageRightsGrant(usageRights, category, right))
        {
            return;
        }

        if (permsValue is PdfObject.Ref reference && Load(reference.ObjectId) is PdfObject.Dict)
        {
            ((PdfObject.Dict)EditAny(reference.ObjectId)).Remove("UR3");
            return;
        }

        var copy = (PdfObject.Dict)perms.DeepClone();
        copy.Remove("UR3");
        EditCatalog().Set("Perms", copy);
    }

    private bool UsageRightsGrant(PdfObject usageRights, string category, string right)
    {
        if (Resolve(usageRights) is not PdfObject.Dict signature
            || ResolveMaybe(signature.Get("Reference")) is not PdfObject.Arr references
            || references.Items.Count == 0)
        {
            return false;
        }

        foreach (PdfObject item in references.Items)
        {
            if (Resolve(item) is not PdfObject.Dict reference
                || NameOf(ResolveMaybe(reference.Get("TransformMethod"))) != "UR3"
                || ResolveMaybe(reference.Get("TransformParams")) is not PdfObject.Dict parameters
                || !IsUsageRightsVersion(parameters.Get("V"))
                || !ListsRight(ResolveMaybe(parameters.Get(category)), right))
            {
                return false;
            }
        }

        return true;
    }

    // ISO 32000-1 Table 255: /V is the name 2.2, which is also its default. An unknown version enables no rights.
    private bool IsUsageRightsVersion(PdfObject? value)
    {
        if (value == null)
        {
            return true;
        }

        return !IsDangling(value) && NameOf(Resolve(value)) == "2.2";
    }

    private bool ListsRight(PdfObject? value, string right)
    {
        if (value is not PdfObject.Arr rights)
        {
            return false;
        }

        bool found = false;
        foreach (PdfObject item in rights.Items)
        {
            string? name = NameOf(Resolve(item));
            if (name == null)
            {
                return false;
            }

            found |= name == right;
        }

        return found;
    }

    private int CertificationLevel()
    {
        int level = 0;
        PdfObject? permsValue = CatalogDict().Get("Perms");
        if (IsDangling(permsValue))
        {
            Restrict(ref level, 1);
        }
        else if (ResolveMaybe(permsValue) is PdfObject.Dict perms && perms.Get("DocMDP") != null)
        {
            Restrict(ref level, PermissionOf(perms.Get("DocMDP")));
        }

        foreach (FieldSlot slot in CollectFields())
        {
            if (slot.FieldType != PdfFieldType.Signature)
            {
                continue;
            }

            if (Load(slot.ValueObjectId) is not PdfObject.Dict field)
            {
                continue;
            }

            if (ResolveMaybe(field.Get("V")) is not PdfObject.Dict signature)
            {
                continue;
            }

            Restrict(ref level, PermissionOnSignature(signature, "DocMDP"));
        }

        return level;
    }

    private int PermissionOf(PdfObject? value)
    {
        if (value is not PdfObject.Ref reference || !_xref.ContainsKey(reference.ObjectId))
        {
            return 1;
        }

        if (ResolveMaybe(reference) is not PdfObject.Dict signature)
        {
            return 1;
        }

        int permission = PermissionOnSignature(signature, "DocMDP");
        return permission == 0 ? 1 : permission;
    }

    private int PermissionOnSignature(PdfObject.Dict signature, string method)
    {
        if (IsDangling(signature.Get("Reference")))
        {
            return 1;
        }

        if (ResolveMaybe(signature.Get("Reference")) is not PdfObject.Arr references)
        {
            return 0;
        }

        int found = 0;
        foreach (PdfObject item in references.Items)
        {
            if (IsDangling(item))
            {
                return 1;
            }

            if (ResolveMaybe(item) is not PdfObject.Dict reference || NameOf(reference.Get("TransformMethod")) != method)
            {
                continue;
            }

            int permission = ReadPermission(reference);
            if (found == 0 || permission < found)
            {
                found = permission;
            }
        }

        return found;
    }

    private int ReadPermission(PdfObject.Dict reference)
    {
        PdfObject? parameters = reference.Get("TransformParams");
        if (IsDangling(parameters))
        {
            return 1;
        }

        PdfObject.Dict? dict = parameters as PdfObject.Dict ?? ResolveMaybe(parameters) as PdfObject.Dict;
        if (dict == null || dict.Get("P") == null)
        {
            return 2;
        }

        int permission = IntOf(dict.Get("P"));
        return permission is >= 1 and <= 3 ? permission : 1;
    }

    private static void Restrict(ref int level, int permission)
    {
        if (permission == 0)
        {
            return;
        }

        if (permission < 1 || permission > 3)
        {
            permission = 1;
        }

        if (level == 0 || permission < level)
        {
            level = permission;
        }
    }

    private bool FieldChangeBlocked(string fullName)
    {
        foreach (FieldSlot slot in CollectFields())
        {
            if (slot.FieldType != PdfFieldType.Signature)
            {
                continue;
            }

            if (Load(slot.ValueObjectId) is not PdfObject.Dict field || ResolveMaybe(field.Get("V")) is not PdfObject.Dict signature)
            {
                continue;
            }

            if (IsDangling(signature.Get("Reference")))
            {
                return true;
            }

            if (ResolveMaybe(signature.Get("Reference")) is not PdfObject.Arr references)
            {
                continue;
            }

            foreach (PdfObject item in references.Items)
            {
                if (IsDangling(item))
                {
                    return true;
                }

                if (ResolveMaybe(item) is not PdfObject.Dict reference || NameOf(reference.Get("TransformMethod")) != "FieldMDP")
                {
                    continue;
                }

                if (FieldMdpBlocks(reference, fullName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool FieldMdpBlocks(PdfObject.Dict reference, string fullName)
    {
        PdfObject? parameters = reference.Get("TransformParams");
        if (IsDangling(parameters) || (ResolveMaybe(parameters) is PdfObject.Dict lockParameters && IsDangling(lockParameters.Get("Fields"))))
        {
            return true;
        }

        PdfObject.Dict? dict = parameters as PdfObject.Dict ?? ResolveMaybe(parameters) as PdfObject.Dict;
        string action = dict == null ? "All" : NameOf(dict.Get("Action")) ?? "All";
        if (action == "All")
        {
            return true;
        }

        var names = new HashSet<string>();
        if (dict != null && ResolveMaybe(dict.Get("Fields")) is PdfObject.Arr fields)
        {
            foreach (PdfObject field in fields.Items)
            {
                if (IsDangling(field))
                {
                    return true;
                }

                string? name = ValueText(field);
                if (name != null)
                {
                    names.Add(name);
                }
            }
        }

        // ISO 32000-1 12.8.2.4: Include locks only the named fields; Exclude locks every field except them.
        // A named field covers its descendants, as pyHanko applies it. An unknown action locks, like an
        // unresolved certification.
        if (action == "Include")
        {
            return Names(names, fullName);
        }

        return action != "Exclude" || !Names(names, fullName);
    }

    private static bool Names(HashSet<string> names, string fullName)
    {
        if (names.Contains(fullName))
        {
            return true;
        }

        for (int dot = fullName.LastIndexOf('.'); dot > 0; dot = fullName.LastIndexOf('.', dot - 1))
        {
            if (names.Contains(fullName.Substring(0, dot)))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryWriteCheckBoxAppearances(List<FieldSlot> matches)
    {
        foreach (FieldSlot slot in matches)
        {
            if (Math.Abs(slot.Rect.Width) <= 0 || Math.Abs(slot.Rect.Height) <= 0)
            {
                return false;
            }
        }

        foreach (FieldSlot slot in matches)
        {
            double width = Math.Abs(slot.Rect.Width);
            double height = Math.Abs(slot.Rect.Height);
            WriteStateAppearances(
                slot.WidgetObjectId,
                "Yes",
                AppearanceBuilder.CheckBox(true, width, height),
                AppearanceBuilder.CheckBox(false, width, height),
                width,
                height);
        }

        return true;
    }

    private bool TryWriteRadioAppearances(List<FieldSlot> matches, string exportValue)
    {
        if (!IsPdfName(exportValue))
        {
            return false;
        }

        foreach (FieldSlot slot in matches)
        {
            if (Math.Abs(slot.Rect.Width) <= 0 || Math.Abs(slot.Rect.Height) <= 0)
            {
                return false;
            }
        }

        foreach (FieldSlot slot in matches)
        {
            double width = Math.Abs(slot.Rect.Width);
            double height = Math.Abs(slot.Rect.Height);
            WriteStateAppearances(
                slot.WidgetObjectId,
                exportValue,
                AppearanceBuilder.Radio(true, width, height),
                AppearanceBuilder.Radio(false, width, height),
                width,
                height);
        }

        return true;
    }

    private bool TryWriteChoiceAppearance(List<FieldSlot> matches, IReadOnlyList<string> values)
    {
        var lines = new List<string>();
        foreach (string value in values)
        {
            string display = value;
            foreach (PdfChoiceOption option in matches[0].Options)
            {
                if (option.Value == value)
                {
                    display = option.Display;
                    break;
                }
            }

            if (!PdfTextCodec.CanEncodeWinAnsi(display))
            {
                return false;
            }

            lines.Add(display);
        }

        foreach (FieldSlot slot in matches)
        {
            if (Math.Abs(slot.Rect.Width) <= 0 || Math.Abs(slot.Rect.Height) <= 0)
            {
                return false;
            }
        }

        string text = string.Join("\n", lines);
        int fontId = EnsureHelv();
        foreach (FieldSlot slot in matches)
        {
            var appearance = AppearanceBuilder.ParseDefaultAppearance(slot.DefaultAppearance);
            byte[] bytes = AppearanceBuilder.Build(
                text,
                Math.Abs(slot.Rect.Width),
                Math.Abs(slot.Rect.Height),
                appearance.Size,
                slot.Align,
                appearance.Red,
                appearance.Green,
                appearance.Blue,
                lines.Count > 1 || slot.Multiline);
            WriteNormalAppearance(slot.WidgetObjectId, AllocateHelvAppearance(bytes, slot, fontId));
        }

        if (!_stickyNeedAppearances)
        {
            SetNeedAppearances(false);
        }

        return true;
    }

    private void WriteStateAppearances(int widgetId, string onName, byte[] onBytes, byte[] offBytes, double width, double height)
    {
        int onId = AllocateFormXObject(onBytes, width, height, false, 0);
        int offId = AllocateFormXObject(offBytes, width, height, false, 0);
        PdfObject.Dict widget = (PdfObject.Dict)EditAny(widgetId);
        var states = new PdfObject.Dict();
        states.Set(onName, new PdfObject.Ref(onId, 0));
        states.Set("Off", new PdfObject.Ref(offId, 0));
        var appearance = new PdfObject.Dict();
        appearance.Set("N", states);
        widget.Set("AP", appearance);
    }

    private void WriteNormalAppearance(int widgetId, int appearanceId)
    {
        PdfObject.Dict widget = (PdfObject.Dict)EditAny(widgetId);
        var normal = new PdfObject.Dict();
        normal.Set("N", new PdfObject.Ref(appearanceId, 0));
        widget.Set("AP", normal);
    }

    private int AllocateHelvAppearance(byte[] bytes, FieldSlot slot, int fontId)
    {
        return AllocateFormXObject(bytes, Math.Abs(slot.Rect.Width), Math.Abs(slot.Rect.Height), true, fontId);
    }

    private int AllocateFormXObject(byte[] bytes, double width, double height, bool helvetica, int fontId)
    {
        var stream = new PdfObject.Dict();
        stream.Set("Type", new PdfObject.Name("XObject"));
        stream.Set("Subtype", new PdfObject.Name("Form"));
        stream.Set("FormType", PdfObject.Num.FromLong(1));
        stream.Set("BBox", RectArray(new PdfRect(0, 0, width, height)));
        if (helvetica)
        {
            var resources = new PdfObject.Dict();
            var fonts = new PdfObject.Dict();
            fonts.Set("Helv", new PdfObject.Ref(fontId, 0));
            resources.Set("Font", fonts);
            stream.Set("Resources", resources);
        }

        stream.Set("Length", PdfObject.Num.FromLong(bytes.Length));
        stream.Body = new PdfObject.MemoryBody(bytes);
        return Allocate(stream);
    }

    private static bool IsPdfName(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (character <= 32 || character >= 127 || character is '#' or '/' or '(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '%')
            {
                return false;
            }
        }

        return true;
    }

    private List<AttachmentSlot> ReadAttachments()
    {
        var list = new List<AttachmentSlot>();
        var seen = new HashSet<int>();
        PdfObject.Dict catalog = CatalogDict();
        if (ResolveMaybe(catalog.Get("Names")) is PdfObject.Dict names)
        {
            PdfObject? embeddedFiles = names.Get("EmbeddedFiles");
            if (ResolveMaybe(embeddedFiles) is PdfObject.Dict tree)
            {
                int treeId = embeddedFiles is PdfObject.Ref reference ? reference.ObjectId : tree.ObjectNumber;
                WalkNames(tree, treeId, list, seen, new HashSet<int>(), 0);
            }
        }

        if (ResolveMaybe(catalog.Get("AF")) is PdfObject.Arr af)
        {
            foreach (PdfObject item in af.Items)
            {
                if (item is PdfObject.Ref reference && seen.Add(reference.ObjectId) && Resolve(reference) is PdfObject.Dict spec)
                {
                    list.Add(ToAttachment(spec, reference.ObjectId));
                }
            }
        }

        return list;
    }

    private void WalkNames(PdfObject.Dict node, int nodeId, List<AttachmentSlot> list, HashSet<int> seen, HashSet<int> seenNodes, int depth)
    {
        if (nodeId != 0 && !seenNodes.Add(nodeId))
        {
            throw new PdfParseException(0, "The embedded-file name tree contains a cycle.");
        }

        if (depth > MaxTraversalDepth)
        {
            throw new PdfParseException(0, "The embedded-file name tree is too deep.");
        }

        if (ResolveMaybe(node.Get("Names")) is PdfObject.Arr names)
        {
            for (int i = 0; i + 1 < names.Items.Count; i += 2)
            {
                if (names.Items[i] is not PdfObject.Str key || ResolveMaybe(names.Items[i + 1]) is not PdfObject.Dict spec)
                {
                    continue;
                }

                int id = names.Items[i + 1] is PdfObject.Ref reference ? reference.ObjectId : spec.ObjectNumber;
                if (id != 0 && !seen.Add(id))
                {
                    continue;
                }

                // A name-tree key is a string, not necessarily a text string (ISO 32000-1 7.9.6). A key that is not
                // text keeps the file specification's name (/UF, then /F); its bytes stay the key on a later write.
                AttachmentSlot slot = ToAttachment(spec, id);
                slot.SortKey = key.Raw;
                if (PdfTextCodec.TryDecode(key.Raw, out string keyText, out _))
                {
                    slot.Name = keyText;
                }

                list.Add(slot);
            }
        }

        if (ResolveMaybe(node.Get("Kids")) is PdfObject.Arr kids)
        {
            foreach (PdfObject kid in kids.Items)
            {
                if (ResolveMaybe(kid) is PdfObject.Dict child)
                {
                    int childId = kid is PdfObject.Ref reference ? reference.ObjectId : child.ObjectNumber;
                    WalkNames(child, childId, list, seen, seenNodes, depth + 1);
                }
            }
        }
    }

    private AttachmentSlot ToAttachment(PdfObject.Dict spec, int specId)
    {
        string name = ValueText(spec.Get("UF")) ?? ValueText(spec.Get("F")) ?? string.Empty;
        byte[] sort = spec.Get("UF") is PdfObject.Str uf ? uf.Raw : spec.Get("F") is PdfObject.Str f ? f.Raw : PdfTextCodec.Encode(name).Raw;
        string? description = ValueText(spec.Get("Desc"));
        int streamId = 0;
        string? mime = null;
        long? size = null;
        if (ResolveMaybe(spec.Get("EF")) is PdfObject.Dict ef)
        {
            PdfObject? file = ef.Get("UF") ?? ef.Get("F");
            if (ResolveMaybe(file) is PdfObject.Dict stream)
            {
                if (file is PdfObject.Ref reference)
                {
                    streamId = reference.ObjectId;
                }

                mime = NameOf(stream.Get("Subtype"));
                if (ResolveMaybe(stream.Get("Params")) is PdfObject.Dict parameters && LongOrNull(parameters.Get("Size")) is long declared)
                {
                    size = declared;
                }
            }
        }

        return new AttachmentSlot(name, sort, specId, description, mime, size, streamId);
    }

    private void WriteAttachments(List<AttachmentSlot> slots)
    {
        slots.Sort((left, right) => CompareBytes(left.SortKey, right.SortKey));
        var names = new PdfObject.Arr();
        var af = new PdfObject.Arr();
        foreach (AttachmentSlot slot in slots)
        {
            bool hex = false;
            foreach (byte b in slot.SortKey)
            {
                if (b < 32 || b > 126)
                {
                    hex = true;
                    break;
                }
            }

            names.Items.Add(new PdfObject.Str(slot.SortKey, hex));
            names.Items.Add(new PdfObject.Ref(slot.SpecId, 0));
            af.Items.Add(new PdfObject.Ref(slot.SpecId, 0));
        }

        var tree = new PdfObject.Dict();
        tree.Set("Names", names);
        PdfObject.Dict catalog = EditCatalog();
        PdfObject.Dict nameDict;
        if (catalog.Get("Names") is PdfObject.Ref nameRef && IsLive(nameRef) && Resolve(nameRef) is PdfObject.Dict)
        {
            nameDict = (PdfObject.Dict)EditAny(nameRef.ObjectId);
        }
        else if (catalog.Get("Names") is PdfObject.Dict inline)
        {
            nameDict = inline;
        }
        else
        {
            nameDict = new PdfObject.Dict();
            catalog.Set("Names", nameDict);
        }

        nameDict.Set("EmbeddedFiles", tree);
        catalog.Set("AF", af);
    }

    private void BuildSignature(PdfSignatureOptions options, DateTimeOffset? signingTime)
    {
        List<FieldSlot> existing = FindNamedFields(options.FieldName);
        if (existing.Count > 0)
        {
            SealExistingField(existing, options, signingTime);
            return;
        }

        List<PageInfo> pages = GetPages();
        if (options.PageNumber < 1 || options.PageNumber > pages.Count)
        {
            throw new PdfSignException("The signature page is outside the document.");
        }

        PageInfo page = pages[options.PageNumber - 1];
        string fieldName = options.FieldName ?? string.Empty;
        if (fieldName.Length == 0)
        {
            var names = new HashSet<string>(CollectFields().Select(slot => slot.FullName));
            int number = 1;
            while (names.Contains("Signature" + number.ToString(CultureInfo.InvariantCulture)))
            {
                number++;
            }

            fieldName = "Signature" + number.ToString(CultureInfo.InvariantCulture);
        }

        int signatureId = Allocate(CreateSignatureDictionary(options, signingTime));
        int pageGen = Load(page.ObjectId).Generation;
        var widget = new PdfObject.Dict();
        widget.Set("Type", new PdfObject.Name("Annot"));
        widget.Set("Subtype", new PdfObject.Name("Widget"));
        widget.Set("FT", new PdfObject.Name("Sig"));
        widget.Set("T", PdfTextCodec.Encode(fieldName));
        widget.Set("V", new PdfObject.Ref(signatureId, 0));
        widget.Set("Rect", RectArray(options.Rect));
        widget.Set("F", PdfObject.Num.FromLong(4));
        widget.Set("P", new PdfObject.Ref(page.ObjectId, pageGen));
        int widgetId = Allocate(widget);
        PdfObject.Dict pageDict = (PdfObject.Dict)EditAny(page.ObjectId);
        EditArray(pageDict, "Annots").Items.Add(new PdfObject.Ref(widgetId, 0));
        PdfObject.Dict acro = EnsureAcroForm();
        EditArray(acro, "Fields").Items.Add(new PdfObject.Ref(widgetId, 0));
        SetSignatureFlags(acro);
    }

    private List<FieldSlot> FindNamedFields(string? fieldName)
    {
        if (string.IsNullOrEmpty(fieldName))
        {
            return new List<FieldSlot>();
        }

        return CollectFields().Where(slot => slot.FullName == fieldName).ToList();
    }

    private void SealExistingField(List<FieldSlot> matches, PdfSignatureOptions options, DateTimeOffset? signingTime)
    {
        foreach (FieldSlot slot in matches)
        {
            if (slot.FieldType != PdfFieldType.Signature)
            {
                throw new PdfSignException("The named field is not a signature field.");
            }
        }

        foreach (FieldSlot slot in matches)
        {
            if (slot.ReadOnly)
            {
                throw new PdfFieldReadOnlyException(slot.FullName);
            }
        }

        foreach (FieldSlot slot in matches)
        {
            if (HasSignatureDictionary(slot))
            {
                throw new PdfSignException("The signature field already has a signature dictionary.");
            }
        }

        int signatureId = Allocate(CreateSignatureDictionary(options, signingTime));
        var owners = new HashSet<int>();
        foreach (FieldSlot slot in matches)
        {
            if (!owners.Add(slot.ValueObjectId))
            {
                continue;
            }

            PdfObject.Dict owner = (PdfObject.Dict)EditAny(slot.ValueObjectId);
            owner.Set("V", new PdfObject.Ref(signatureId, 0));
        }

        SetSignatureFlags(EnsureAcroForm());
    }

    private static void SetSignatureFlags(PdfObject.Dict acro)
    {
        acro.Set("SigFlags", PdfObject.Num.FromLong(IntOf(acro.Get("SigFlags")) | 3));
    }

    private static PdfObject.Dict CreateSignatureDictionary(PdfSignatureOptions options, DateTimeOffset? signingTime)
    {
        var signature = new PdfObject.Dict();
        if (options.Kind == PdfSignatureKind.DocTimeStamp)
        {
            signature.Set("Type", new PdfObject.Name("DocTimeStamp"));
            signature.Set("Filter", new PdfObject.Name("Adobe.PPKLite"));
            signature.Set("SubFilter", new PdfObject.Name("ETSI.RFC3161"));
        }
        else
        {
            signature.Set("Type", new PdfObject.Name("Sig"));
            signature.Set("Filter", new PdfObject.Name("Adobe.PPKLite"));
            signature.Set("SubFilter", new PdfObject.Name("adbe.pkcs7.detached"));
            SetOptional(signature, "Reason", options.Reason);
            SetOptional(signature, "Location", options.Location);
            SetOptional(signature, "ContactInfo", options.ContactInfo);
            SetOptional(signature, "Name", options.SignerName);
            signature.Set("M", PdfTextCodec.Encode(PdfTextCodec.FormatDate(signingTime ?? DateTimeOffset.UtcNow)));
        }

        signature.Set("ByteRange", new PdfObject.ByteRangeSlot());
        signature.Set("Contents", new PdfObject.ContentsSlot(options.ReservedContentsBytes));
        return signature;
    }

    private bool HasSignatureDictionary(FieldSlot slot)
    {
        if (Load(slot.ValueObjectId) is PdfObject.Dict owner && ResolveMaybe(owner.Get("V")) is PdfObject.Dict)
        {
            return true;
        }

        return NearestFieldValue(slot.WidgetObjectId) is PdfObject.Dict;
    }

    private PdfObject? NearestFieldValue(int objectId)
    {
        var seen = new HashSet<int>();
        while (seen.Add(objectId))
        {
            if (Load(objectId) is not PdfObject.Dict dict)
            {
                return null;
            }

            PdfObject? value = dict.Get("V");
            if (value != null)
            {
                return ResolveMaybe(value);
            }

            if (dict.Get("Parent") is not PdfObject.Ref parent || !IsLive(parent))
            {
                return null;
            }

            objectId = parent.ObjectId;
        }

        return null;
    }

    private PdfObject.Arr EditArray(PdfObject.Dict parent, string key)
    {
        if (parent.Get(key) is PdfObject.Ref reference && IsLive(reference))
        {
            return (PdfObject.Arr)EditAny(reference.ObjectId);
        }

        if (parent.Get(key) is PdfObject.Arr array)
        {
            return array;
        }

        var created = new PdfObject.Arr();
        parent.Set(key, created);
        return created;
    }

    private AppendixLayout WriteAppendix(Stream output, long origin)
    {
        var layout = new AppendixLayout();
        var serializer = new PdfSerializer(output, CopyFileBody, _security);
        foreach (int id in _dirty.OrderBy(static value => value))
        {
            PdfObject obj = _cache[id];
            long absolute = origin + output.Position;
            if (absolute > MaxClassicOffset)
            {
                throw new PdfTooLargeForClassicXrefException();
            }

            layout.Objects.Add(new WrittenObject(id, absolute, obj.Generation));
            serializer.WriteObject(obj);
        }

        layout.XrefOffset = origin + output.Position;
        if (layout.XrefOffset > MaxClassicOffset)
        {
            throw new PdfTooLargeForClassicXrefException();
        }

        WriteAscii(output, "xref\r\n");
        List<WrittenObject> items = layout.Objects.OrderBy(item => item.Id).ToList();
        int index = 0;
        while (index < items.Count)
        {
            int start = items[index].Id;
            int count = 1;
            while (index + count < items.Count && items[index + count].Id == start + count)
            {
                count++;
            }

            WriteAscii(output, start.ToString(CultureInfo.InvariantCulture) + " " + count.ToString(CultureInfo.InvariantCulture) + "\r\n");
            for (int i = 0; i < count; i++)
            {
                WrittenObject item = items[index + i];
                string offsetText = item.Offset.ToString("D10", CultureInfo.InvariantCulture);
                string generationText = item.Generation.ToString("D5", CultureInfo.InvariantCulture);
                if (offsetText.Length != 10 || generationText.Length != 5)
                {
                    throw new PdfTooLargeForClassicXrefException();
                }

                WriteAscii(output, offsetText + " " + generationText + " n\r\n");
            }

            index += count;
        }

        WriteAscii(output, "trailer\n<<\n/Size " + _size.ToString(CultureInfo.InvariantCulture) + "\n/Prev " + _startXref.ToString(CultureInfo.InvariantCulture) + "\n/Root " + _rootId.ToString(CultureInfo.InvariantCulture) + " " + _rootGen.ToString(CultureInfo.InvariantCulture) + " R\n");
        if (_infoId != null)
        {
            WriteAscii(output, "/Info " + _infoId.Value.ToString(CultureInfo.InvariantCulture) + " " + _infoGen.ToString(CultureInfo.InvariantCulture) + " R\n");
        }

        if (_encryptId != 0)
        {
            WriteAscii(output, "/Encrypt " + _encryptId.ToString(CultureInfo.InvariantCulture) + " " + _encryptGen.ToString(CultureInfo.InvariantCulture) + " R\n");
        }

        if (_id != null)
        {
            WriteAscii(output, "/ID ");
            serializer.WriteValue(_id);
            WriteAscii(output, "\n");
        }

        WriteAscii(output, ">>\nstartxref\n" + layout.XrefOffset.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
        layout.ByteRange = serializer.ByteRange;
        layout.Contents = serializer.Contents;
        return layout;
    }

    private void FinishWrite(Stream destination, bool leaveOpen, bool same, AppendixLayout layout)
    {
        foreach (WrittenObject item in layout.Objects)
        {
            _xref[item.Id] = new XrefEntry(1, item.Offset, item.Generation);
        }

        _startXref = layout.XrefOffset;
        _dirty.Clear();
        if (same)
        {
            _length = destination.Length;
            return;
        }

        SwitchStream(destination, leaveOpen);
    }

    private void SwitchStream(Stream destination, bool leaveOpen)
    {
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }

        _stream = destination;
        _leaveOpen = leaveOpen;
        _length = destination.Length;
        _cursor = new PdfCursor(_stream);
    }

    private void ValidateDestination(Stream destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (!destination.CanWrite || !destination.CanSeek)
        {
            throw new ArgumentException("The destination must be a writable seekable stream.", nameof(destination));
        }

        if (ReferenceEquals(destination, _stream))
        {
            if (_stream.Length != _length)
            {
                throw new PdfException("The source stream length changed under the document.");
            }

            return;
        }

        if (destination.Length != 0)
        {
            throw new PdfException("The destination stream must be empty.");
        }
    }

    private void CopyPrefix(Stream destination)
    {
        _stream.Seek(0, SeekOrigin.Begin);
        var buffer = new byte[1024 * 1024];
        long remaining = _length;
        while (remaining > 0)
        {
            int count = (int)Math.Min(buffer.Length, remaining);
            int read = _stream.Read(buffer, 0, count);
            if (read == 0)
            {
                throw new EndOfStreamException("The PDF stream ended before its declared length.");
            }

            destination.Write(buffer, 0, read);
            remaining -= read;
        }
    }

    private void CopyFileBody(PdfObject.FileBody body, Stream destination)
    {
        long writeAt = destination.Position;
        long readAt = body.Offset;
        long remaining = body.Length;
        var buffer = new byte[65536];
        while (remaining > 0)
        {
            int count = (int)Math.Min(buffer.Length, remaining);
            _stream.Seek(readAt, SeekOrigin.Begin);
            int read = _stream.Read(buffer, 0, count);
            if (read == 0)
            {
                throw new EndOfStreamException("A PDF stream object ended early.");
            }

            destination.Seek(writeAt, SeekOrigin.Begin);
            destination.Write(buffer, 0, read);
            readAt += read;
            writeAt += read;
            remaining -= read;
        }

        destination.Seek(writeAt, SeekOrigin.Begin);
    }

    private bool NeedsLeadingLineFeed()
    {
        if (_length == 0)
        {
            return false;
        }

        _stream.Seek(_length - 1, SeekOrigin.Begin);
        return _stream.ReadByte() != '\n';
    }

    private Stream OpenSigned(MemoryStream appendix, long lessThan, long gap)
    {
        return new SplicedStream(new[]
        {
            new SplicedStream.Piece(_stream, 0, _length),
            new SplicedStream.Piece(appendix, 0, lessThan),
            new SplicedStream.Piece(appendix, lessThan + gap, appendix.Length - lessThan - gap),
        });
    }

    private Stream OpenBody(PdfObject.Dict dict)
    {
        if (dict.Body is PdfObject.MemoryBody memory)
        {
            return new MemoryStream(memory.Data, false);
        }

        if (dict.Body is PdfObject.ExternalBody external)
        {
            return new PdfSliceStream(external.Source, external.Start, external.Length);
        }

        if (dict.Body is not PdfObject.FileBody file)
        {
            throw new PdfParseException(0, "The stream has no bytes.");
        }

        List<string> filters = PdfStreamDecoder.FiltersOf(dict, Resolve);
        List<PdfObject.Dict?> parms = PdfStreamDecoder.ParmsOf(dict, filters.Count, Resolve);
        return PdfStreamDecoder.OpenDecoded(OpenStored(dict, file), filters, parms);
    }

    private byte[] ReadDecoded(PdfObject.Dict dict, int cap)
    {
        if (dict.Body is PdfObject.MemoryBody memory)
        {
            return memory.Data;
        }

        if (dict.Body is not PdfObject.FileBody file)
        {
            throw new PdfParseException(0, "The stream has no bytes.");
        }

        if (file.Length > cap)
        {
            throw new PdfObjectTooLargeException("The stream is larger than " + cap.ToString(CultureInfo.InvariantCulture) + " bytes.");
        }

        byte[] raw = ReadRange(file.Offset, (int)file.Length);
        if (_security != null && _security.EncryptsStream(dict))
        {
            raw = _security.Decrypt(raw, dict);
        }

        List<string> filters = PdfStreamDecoder.FiltersOf(dict, Resolve);
        if (filters.Count == 0)
        {
            return raw;
        }

        return PdfStreamDecoder.Decode(raw, filters, PdfStreamDecoder.ParmsOf(dict, filters.Count, Resolve), cap);
    }

    private byte[] ReadRange(long offset, int count)
    {
        var buffer = new byte[count];
        int readTotal = 0;
        while (readTotal < count)
        {
            _stream.Seek(offset + readTotal, SeekOrigin.Begin);
            int read = _stream.Read(buffer, readTotal, Math.Min(65536, count - readTotal));
            if (read == 0)
            {
                throw new PdfParseException(offset, "Unexpected end of file.");
            }

            readTotal += read;
        }

        return buffer;
    }

    private bool IsClassicXref(long offset)
    {
        _cursor.Seek(offset);
        while (true)
        {
            int value = _cursor.PeekByte();
            if (value < 0)
            {
                _cursor.Seek(offset);
                return false;
            }

            if (value == '%')
            {
                _cursor.ReadByte();
                while (true)
                {
                    int comment = _cursor.ReadByte();
                    if (comment < 0 || comment == '\n' || comment == '\r')
                    {
                        break;
                    }
                }

                continue;
            }

            if (value is 0 or 9 or 10 or 12 or 13 or 32)
            {
                _cursor.ReadByte();
                continue;
            }

            break;
        }

        bool match = _cursor.ReadByte() == 'x' && _cursor.ReadByte() == 'r' && _cursor.ReadByte() == 'e' && _cursor.ReadByte() == 'f';
        _cursor.Seek(offset);
        return match;
    }

    private void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PdfDocument));
        }
    }

    private static void SetOptional(PdfObject.Dict dict, string key, string? value)
    {
        if (value != null)
        {
            dict.Set(key, PdfTextCodec.Encode(value));
        }
    }

    private static PdfObject.Arr RectArray(PdfRect rect)
    {
        var array = new PdfObject.Arr();
        array.Items.Add(Number(rect.Llx));
        array.Items.Add(Number(rect.Lly));
        array.Items.Add(Number(rect.Urx));
        array.Items.Add(Number(rect.Ury));
        return array;
    }

    private static PdfObject.Num Number(double value)
    {
        if (Math.Abs(value - Math.Truncate(value)) < 0.000000001 && value >= long.MinValue && value <= long.MaxValue)
        {
            return PdfObject.Num.FromLong((long)value);
        }

        return PdfObject.Num.FromDouble(value);
    }

    private static string? NameOf(PdfObject? value)
    {
        return value is PdfObject.Name name ? name.Value : null;
    }

    private string? ValueText(PdfObject? value)
    {
        value = ResolveMaybe(value);
        if (value is PdfObject.Name name)
        {
            return name.Value;
        }

        if (value is PdfObject.Str str)
        {
            return PdfTextCodec.Decode(str.Raw);
        }

        if (value is PdfObject.Arr array && array.Items.Count > 0)
        {
            return ValueText(array.Items[0]);
        }

        return null;
    }

    private List<string> ReadSelected(PdfObject? value)
    {
        var selected = new List<string>();
        value = ResolveMaybe(value);
        if (value is PdfObject.Arr array)
        {
            foreach (PdfObject item in array.Items)
            {
                string? text = ValueText(item);
                if (text != null)
                {
                    selected.Add(text);
                }
            }

            return selected;
        }

        string? single = ValueText(value);
        if (single != null)
        {
            selected.Add(single);
        }

        return selected;
    }

    private List<PdfChoiceOption> ReadOptions(PdfObject? value)
    {
        var options = new List<PdfChoiceOption>();
        if (ResolveMaybe(value) is not PdfObject.Arr array)
        {
            return options;
        }

        foreach (PdfObject item in array.Items)
        {
            PdfObject resolved = Resolve(item);
            if (resolved is PdfObject.Arr pair && pair.Items.Count > 0)
            {
                string export = ValueText(pair.Items[0]) ?? string.Empty;
                string display = pair.Items.Count > 1 ? ValueText(pair.Items[1]) ?? export : export;
                options.Add(new PdfChoiceOption(export, display));
                continue;
            }

            string single = ValueText(resolved) ?? string.Empty;
            options.Add(new PdfChoiceOption(single, single));
        }

        return options;
    }

    private static int ObjectStreamInteger(PdfObject.Dict dict, string key)
    {
        if (dict.Get(key) is not PdfObject.Num number || !number.IsInteger || number.LongValue < 0 || number.LongValue > int.MaxValue)
        {
            throw new PdfParseException(0, "Object stream /" + key + " is not a non-negative 32-bit integer.");
        }

        return (int)number.LongValue;
    }

    private static int IntOf(PdfObject? value)
    {
        if (value is PdfObject.Num number)
        {
            return number.IsInteger ? (int)number.LongValue : (int)number.DoubleValue;
        }

        return 0;
    }

    private static long? LongOrNull(PdfObject? value)
    {
        if (value is not PdfObject.Num number)
        {
            return null;
        }

        return number.IsInteger ? number.LongValue : (long)number.DoubleValue;
    }

    private PdfRect? RectOf(PdfObject? value)
    {
        if (ResolveMaybe(value) is not PdfObject.Arr array || array.Items.Count < 4)
        {
            return null;
        }

        return new PdfRect(DoubleOf(array.Items[0]), DoubleOf(array.Items[1]), DoubleOf(array.Items[2]), DoubleOf(array.Items[3]));
    }

    private double DoubleOf(PdfObject value)
    {
        value = Resolve(value);
        if (value is PdfObject.Num number)
        {
            return number.IsInteger ? number.LongValue : number.DoubleValue;
        }

        return 0;
    }

    private string? OnState(PdfObject.Dict widget)
    {
        if (ResolveMaybe(widget.Get("AP")) is not PdfObject.Dict ap)
        {
            return null;
        }

        if (ResolveMaybe(ap.Get("N")) is not PdfObject.Dict normal)
        {
            return null;
        }

        foreach (var item in normal.Items)
        {
            if (item.Key != "Off")
            {
                return item.Key;
            }
        }

        return null;
    }

    private static string AsciiFallback(string name)
    {
        var chars = new char[name.Length];
        for (int i = 0; i < name.Length; i++)
        {
            chars[i] = name[i] >= 32 && name[i] <= 126 ? name[i] : '_';
        }

        return new string(chars);
    }

    private static PdfObject.Arr NewId()
    {
        var bytes = new byte[16];
        using (RandomNumberGenerator random = RandomNumberGenerator.Create())
        {
            random.GetBytes(bytes);
        }

        var array = new PdfObject.Arr();
        array.Items.Add(new PdfObject.Str(bytes, true));
        array.Items.Add(new PdfObject.Str((byte[])bytes.Clone(), true));
        return array;
    }

    private static void WriteAscii(Stream output, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        output.Write(bytes, 0, bytes.Length);
    }

    private static void PatchAscii(Stream stream, long position, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        stream.Position = position;
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string Ten(long value)
    {
        if (value < 0 || value > MaxClassicOffset)
        {
            throw new PdfTooLargeForClassicXrefException();
        }

        return value.ToString("D10", CultureInfo.InvariantCulture);
    }

    private static int CompareBytes(byte[] left, byte[] right)
    {
        int count = Math.Min(left.Length, right.Length);
        for (int i = 0; i < count; i++)
        {
            int compare = left[i].CompareTo(right[i]);
            if (compare != 0)
            {
                return compare;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    private static int LastIndexOf(byte[] data, byte[] needle, int start)
    {
        if (start >= data.Length)
        {
            start = data.Length - 1;
        }

        for (int i = start; i >= 0; i--)
        {
            if (i + needle.Length > data.Length)
            {
                continue;
            }

            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (data[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryParseStartXref(byte[] data, int index, out long value)
    {
        value = 0;
        while (index < data.Length && (data[index] is 0 or 9 or 10 or 12 or 13 or 32))
        {
            index++;
        }

        int start = index;
        while (index < data.Length && data[index] >= (byte)'0' && data[index] <= (byte)'9')
        {
            index++;
        }

        if (start == index || !long.TryParse(Encoding.ASCII.GetString(data, start, index - start), NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        while (index < data.Length && (data[index] is 0 or 9 or 10 or 12 or 13 or 32))
        {
            index++;
        }

        byte[] eof = Encoding.ASCII.GetBytes("%%EOF");
        if (index + eof.Length > data.Length)
        {
            return false;
        }

        for (int i = 0; i < eof.Length; i++)
        {
            if (data[index + i] != eof[i])
            {
                return false;
            }
        }

        return true;
    }

    private readonly struct ObjectStreamLease : IDisposable
    {
        private readonly PdfEngine _engine;

        public ObjectStreamLease(PdfEngine engine)
        {
            _engine = engine;
        }

        public void Dispose()
        {
            _engine.ReleaseCleanObjectStreams(null, null);
        }
    }

    private sealed class AppendixLayout
    {
        public List<WrittenObject> Objects { get; } = new List<WrittenObject>();

        public long XrefOffset { get; set; }

        public PdfObject.ByteRangeSlot? ByteRange { get; set; }

        public PdfObject.ContentsSlot? Contents { get; set; }
    }

    private readonly struct WrittenObject
    {
        public WrittenObject(int id, long offset, int generation)
        {
            Id = id;
            Offset = offset;
            Generation = generation;
        }

        public int Id { get; }

        public long Offset { get; }

        public int Generation { get; }
    }

    private readonly struct PageInfo
    {
        public PageInfo(int objectId, PdfRect mediaBox, PdfRect cropBox, int rotate)
        {
            ObjectId = objectId;
            MediaBox = mediaBox;
            CropBox = cropBox;
            Rotate = rotate;
            Number = 0;
        }

        public int ObjectId { get; }

        public PdfRect MediaBox { get; }

        public PdfRect CropBox { get; }

        public int Rotate { get; }

        public int Number { get; }
    }

    private readonly struct Inherited
    {
        public Inherited(string? ft, PdfObject? value, int flags, string? da, int align, int? maxLen, int valueOwner, PdfObject? opt)
        {
            Ft = ft;
            Value = value;
            Flags = flags;
            Da = da;
            Align = align;
            MaxLen = maxLen;
            ValueOwner = valueOwner;
            Opt = opt;
        }

        public string? Ft { get; }

        public PdfObject? Value { get; }

        public int Flags { get; }

        public string? Da { get; }

        public int Align { get; }

        public int? MaxLen { get; }

        public int ValueOwner { get; }

        public PdfObject? Opt { get; }
    }

    private sealed class FieldSlot
    {
        public string PartialName { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public int WidgetIndex { get; set; }

        public int ValueObjectId { get; set; }

        public int WidgetObjectId { get; set; }

        public PdfFieldType FieldType { get; set; }

        public PdfButtonKind ButtonKind { get; set; }

        public string? Value { get; set; }

        public string? ExportValue { get; set; }

        public PdfRect Rect { get; set; }

        public int PageNumber { get; set; }

        public int PageRotation { get; set; }

        public PdfRect MediaBox { get; set; }

        public bool ReadOnly { get; set; }

        public bool Required { get; set; }

        public bool Multiline { get; set; }

        public int? MaxLength { get; set; }

        public bool Password { get; set; }

        public bool FileSelect { get; set; }

        public bool Comb { get; set; }

        public bool RichText { get; set; }

        public bool Editable { get; set; }

        public bool MultiSelect { get; set; }

        public IReadOnlyList<string> SelectedValues { get; set; } = Array.Empty<string>();

        public IReadOnlyList<PdfChoiceOption> Options { get; set; } = Array.Empty<PdfChoiceOption>();

        public string? DefaultAppearance { get; set; }

        public int Align { get; set; }
    }

    private sealed class AttachmentSlot
    {
        public AttachmentSlot(string name, byte[] sortKey, int specId, string? description, string? mimeType, long? size, int streamId)
        {
            Name = name;
            SortKey = sortKey;
            SpecId = specId;
            Description = description;
            MimeType = mimeType;
            Size = size;
            StreamId = streamId;
        }

        public string Name { get; set; }

        public byte[] SortKey { get; set; }

        public int SpecId { get; }

        public string? Description { get; }

        public string? MimeType { get; }

        public long? Size { get; }

        public int StreamId { get; }
    }
}
