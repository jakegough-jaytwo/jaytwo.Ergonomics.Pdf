using System;
using System.Collections.Generic;
using System.Text;

namespace jaytwo.Pdf.Internal;

internal sealed class PdfAssemblyLinks
{
    private static readonly string[] Keys = { "Rect", "Border", "BS", "C", "H", "F", "QuadPoints", "AP", "AS" };
    private readonly PdfObjectStore _store;
    private readonly PdfNewFileBudget _budget;
    private readonly PdfObject.Dict _catalog;
    private readonly Dictionary<int, int> _sourcePages;
    private readonly Dictionary<int, PdfObject.Ref> _firstPages;
    private readonly PdfExternalLinkMode _externalLinks;

    internal PdfAssemblyLinks(
        PdfObjectStore store,
        PdfNewFileBudget budget,
        PdfObject.Dict catalog,
        Dictionary<int, int> sourcePages,
        Dictionary<int, PdfObject.Ref> firstPages,
        PdfExternalLinkMode externalLinks)
    {
        _store = store;
        _budget = budget;
        _catalog = catalog;
        _sourcePages = sourcePages;
        _firstPages = firstPages;
        _externalLinks = externalLinks;
    }

    internal PdfObject.Dict? Rebuild(PdfObject.Dict annotation, int sourcePage, PdfObject.Ref outputPage, out PdfOmissionReason reason)
    {
        reason = PdfOmissionReason.UnsupportedLink;
        if (annotation.Get("AA") != null)
        {
            return null;
        }

        var link = new PdfObject.Dict();
        link.Set("Type", new PdfObject.Name("Annot"));
        link.Set("Subtype", new PdfObject.Name("Link"));
        foreach (string key in Keys)
        {
            if (annotation.Get(key) is PdfObject value)
            {
                link.Set(key, value);
            }
        }

        PdfObject? destination = annotation.Get("Dest");
        if (annotation.Get("A") is PdfObject actionValue)
        {
            if (destination != null || _store.Resolve(actionValue) is not PdfObject.Dict action || action.Get("Next") != null)
            {
                return null;
            }

            string? kind = PdfValues.NameOf(_store.ResolveMaybe(action.Get("S")));
            if (kind == "URI")
            {
                if (_externalLinks == PdfExternalLinkMode.Drop)
                {
                    reason = PdfOmissionReason.ExternalLinkDropped;
                    return null;
                }

                if ((action.Get("IsMap") != null && _store.Resolve(action.Get("IsMap")!) is not PdfObject.Bool { Value: false })
                    || _store.ResolveMaybe(action.Get("URI")) is not PdfObject.Str text
                    || !TryUri(Encoding.UTF8.GetString(text.Raw), out string uri))
                {
                    return null;
                }

                var rebuilt = new PdfObject.Dict();
                rebuilt.Set("S", new PdfObject.Name("URI"));
                rebuilt.Set("URI", new PdfObject.Str(Encoding.UTF8.GetBytes(uri), false));
                link.Set("A", rebuilt);
                return link;
            }

            if (kind != "GoTo")
            {
                return null;
            }

            destination = action.Get("D");
        }

        reason = PdfOmissionReason.MissingDestination;
        PdfObject.Arr? remapped = Destination(destination, sourcePage, outputPage);
        if (remapped == null)
        {
            return null;
        }

        link.Set("Dest", remapped);
        return link;
    }

    private bool TryUri(string text, out string absolute)
    {
        absolute = string.Empty;
        // Reject characters which System.Uri would silently trim, escape, or reinterpret.
        foreach (char character in text)
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character) || character == '\\')
            {
                return false;
            }
        }

        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '%' && (index + 2 >= text.Length || !Uri.IsHexDigit(text[index + 1]) || !Uri.IsHexDigit(text[index + 2])))
            {
                return false;
            }
        }

        Uri? uri;
        int colon = text.IndexOf(':');
        bool hasScheme = colon > 0 && Uri.CheckSchemeName(text.Substring(0, colon));
        if (!hasScheme || !Uri.TryCreate(text, UriKind.Absolute, out uri))
        {
            PdfObject.Dict? dictionary = _store.ResolveMaybe(_catalog.Get("URI")) as PdfObject.Dict;
            if (_store.ResolveMaybe(dictionary?.Get("Base")) is not PdfObject.Str baseText
                || !Uri.TryCreate(Encoding.UTF8.GetString(baseText.Raw), UriKind.Absolute, out Uri? baseUri)
                || !WebUri(baseUri) || !baseUri.IsWellFormedOriginalString()
                || !Uri.TryCreate(baseUri, text, out uri))
            {
                return false;
            }
        }

        if (!uri.IsWellFormedOriginalString() || (!WebUri(uri) && uri.Scheme != "mailto"))
        {
            return false;
        }

        absolute = uri.AbsoluteUri;
        return true;
    }

    private bool WebUri(Uri uri)
    {
        return (uri.Scheme == "http" || uri.Scheme == "https") && uri.UserInfo.Length == 0 && uri.Host.Length > 0;
    }

    private PdfObject.Arr? Destination(PdfObject? value, int sourcePage, PdfObject.Ref outputPage)
    {
        value = _store.ResolveMaybe(value);
        if (value is PdfObject.Name name)
        {
            value = (_store.ResolveMaybe(_catalog.Get("Dests")) as PdfObject.Dict)?.Get(name.Value);
        }
        else if (value is PdfObject.Str text)
        {
            PdfObject.Dict? names = _store.ResolveMaybe(_catalog.Get("Names")) as PdfObject.Dict;
            value = FindName(names?.Get("Dests"), text.Raw, new HashSet<PdfObject>(), 0);
        }

        value = _store.ResolveMaybe(value);
        if (value is PdfObject.Dict dictionary)
        {
            value = _store.ResolveMaybe(dictionary.Get("D"));
        }

        if (value is not PdfObject.Arr array || array.Items.Count < 2 || array.Items[0] is not PdfObject.Ref target
            || _store.IsDangling(target) || !_sourcePages.TryGetValue(target.ObjectId, out int pageNumber)
            || !_firstPages.TryGetValue(pageNumber, out PdfObject.Ref? first))
        {
            return null;
        }

        string? mode = PdfValues.NameOf(_store.ResolveMaybe(array.Items[1]));
        int count = mode switch
        {
            "XYZ" => 5,
            "Fit" or "FitB" => 2,
            "FitH" or "FitV" or "FitBH" or "FitBV" => 3,
            "FitR" => 6,
            _ => 0,
        };
        if (count == 0 || array.Items.Count != count)
        {
            return null;
        }

        var result = new PdfObject.Arr(count);
        result.Items.Add(pageNumber == sourcePage ? outputPage : first);
        result.Items.Add(new PdfObject.Name(mode!));
        for (int index = 2; index < count; index++)
        {
            PdfObject item = _store.Resolve(array.Items[index]);
            if (item is not (PdfObject.Num or PdfObject.NullObj))
            {
                return null;
            }

            result.Items.Add(item.DeepClone());
        }

        return result;
    }

    private PdfObject? FindName(PdfObject? value, byte[] key, HashSet<PdfObject> seen, int depth)
    {
        if (value == null)
        {
            return null;
        }

        if (depth > _budget.TraversalDepth)
        {
            throw new PdfResourceLimitException("destination name-tree depth");
        }

        PdfObject node = _store.Resolve(value);
        _budget.CountMappedObject();
        if (!seen.Add(node) || node is not PdfObject.Dict dictionary)
        {
            throw new PdfParseException(0, "Invalid or cyclic destination name tree.");
        }

        if (_store.ResolveMaybe(dictionary.Get("Names")) is PdfObject.Arr names)
        {
            for (int index = 0; index + 1 < names.Items.Count; index += 2)
            {
                _budget.CountMappedObject();
                if (_store.Resolve(names.Items[index]) is PdfObject.Str name && EqualBytes(name.Raw, key))
                {
                    return names.Items[index + 1];
                }
            }
        }

        if (_store.ResolveMaybe(dictionary.Get("Kids")) is PdfObject.Arr kids)
        {
            foreach (PdfObject child in kids.Items)
            {
                PdfObject? found = FindName(child, key, seen, depth + 1);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private bool EqualBytes(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }
}
