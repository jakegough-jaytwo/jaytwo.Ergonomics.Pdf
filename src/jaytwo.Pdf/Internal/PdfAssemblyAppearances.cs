using System.Collections.Generic;

namespace jaytwo.Pdf.Internal;

// Classify and bound the complete retained graph before registering any annotation-owned object.
internal sealed class PdfAssemblyAppearances
{
    internal const int MaxObjects = 4096;
    internal const int MaxDepth = 32;
    internal const long MaxBytes = 8L * 1024 * 1024;

    private static readonly HashSet<string> ResourceKeys = new HashSet<string>
    {
        "Type", "Subtype", "FormType", "Length", "Filter", "DecodeParms", "BBox", "Matrix", "Resources", "Group",
        "Font", "XObject", "ExtGState", "ColorSpace", "Pattern", "Shading", "Properties", "ProcSet",
        "BaseFont", "Name", "Encoding", "FirstChar", "LastChar", "Widths", "FontDescriptor", "ToUnicode",
        "DescendantFonts", "CIDSystemInfo", "Registry", "Ordering", "Supplement", "DW", "W", "DW2", "W2", "CIDToGIDMap",
        "FontName", "Flags", "FontBBox", "ItalicAngle", "Ascent", "Descent", "CapHeight", "XHeight", "StemV", "StemH",
        "AvgWidth", "MaxWidth", "MissingWidth", "FontFile", "FontFile2", "FontFile3", "CharSet", "FontFamily", "FontStretch", "FontWeight",
        "Length1", "Length2", "Length3", "BaseEncoding", "Differences", "FontMatrix", "CharProcs", "PaintType",
        "Width", "Height", "BitsPerComponent", "Decode", "ImageMask", "Mask", "SMask", "Interpolate", "Intent", "Matte",
        "S", "CS", "I", "K", "AIS", "BM", "CA", "ca", "LW", "LC", "LJ", "ML", "D", "RI", "OP", "op", "OPM",
        "Font", "BG", "BG2", "UCR", "UCR2", "TR", "TR2", "HT", "FL", "SM", "SA", "TK", "BC", "G",
        "FunctionType", "Domain", "Range", "Size", "BitsPerSample", "Order", "Encode", "C0", "C1", "N", "Functions", "Bounds",
        "PatternType", "TilingType", "XStep", "YStep", "ShadingType", "Coords", "Function", "Extend", "Background", "AntiAlias",
        "BitsPerCoordinate", "BitsPerComponent", "BitsPerFlag", "VerticesPerRow", "Alternate", "Metadata", "AF",
        "Columns", "Colors", "Predictor", "EarlyChange", "Rows", "K", "EndOfLine", "EncodedByteAlign", "EndOfBlock", "BlackIs1",
    };

    private readonly PdfObjectStore _store;
    private readonly PdfNewFileBudget _budget;
    private long _bytes;

    internal PdfAssemblyAppearances(PdfObjectStore store, PdfNewFileBudget budget)
    {
        _store = store;
        _budget = budget;
    }

    internal bool Supported(PdfObject? value)
    {
        if (value == null)
        {
            return true;
        }

        _bytes = 0;
        if (_store.IsDangling(value))
        {
            throw new PdfParseException(0, "An appearance reference is unresolved.");
        }

        if (_store.Resolve(value) is not PdfObject.Dict appearances || appearances.Body != null || appearances.Get("N") == null)
        {
            return false;
        }

        foreach (var item in appearances.Items)
        {
            if (item.Key is not ("N" or "R" or "D") || !Appearance(item.Value))
            {
                return false;
            }
        }

        return Inspect(value, new HashSet<PdfObject>(), 0, true);
    }

    private bool Appearance(PdfObject value)
    {
        if (_store.IsDangling(value))
        {
            throw new PdfParseException(0, "An appearance reference is unresolved.");
        }

        if (_store.Resolve(value) is not PdfObject.Dict dictionary)
        {
            return false;
        }

        if (dictionary.Body != null)
        {
            return PdfValues.NameOf(_store.ResolveMaybe(dictionary.Get("Subtype"))) == "Form"
                && _store.ResolveMaybe(dictionary.Get("BBox")) is PdfObject.Arr { Items.Count: 4 };
        }

        if (dictionary.Items.Count > MaxObjects)
        {
            return false;
        }

        foreach (var state in dictionary.Items)
        {
            if (_store.IsDangling(state.Value))
            {
                throw new PdfParseException(0, "An appearance state reference is unresolved.");
            }

            if (_store.Resolve(state.Value) is not PdfObject.Dict stream || stream.Body == null || !Appearance(state.Value))
            {
                return false;
            }
        }

        return dictionary.Items.Count > 0;
    }

    private bool Inspect(PdfObject value, HashSet<PdfObject> seen, int depth, bool names = false)
    {
        if (_store.IsDangling(value))
        {
            throw new PdfParseException(0, "An appearance resource reference is unresolved.");
        }

        PdfObject resolved = _store.Resolve(value);
        if (!seen.Add(resolved))
        {
            return true;
        }

        _budget.CountMappedObject();
        if (depth > _budget.TraversalDepth)
        {
            throw new PdfResourceLimitException("appearance traversal depth");
        }

        if (seen.Count > MaxObjects || depth > MaxDepth)
        {
            return false;
        }

        if (resolved is PdfObject.Dict dictionary)
        {
            if (PdfValues.NameOf(_store.ResolveMaybe(dictionary.Get("Type"))) is "Catalog" or "Page" or "Pages" or "Annot" or "Sig" or "Filespec" or "EmbeddedFile"
                || dictionary.Get("FT") != null || dictionary.Get("A") != null || dictionary.Get("AA") != null
                || dictionary.Get("OC") != null)
            {
                return false;
            }

            if (dictionary.Get("AF") is PdfObject association && _store.Resolve(association) is not PdfObject.Arr { Items.Count: 0 })
            {
                throw new PdfAssemblyNotSupportedException("Assembly refuses associated appearance files.");
            }

            if (dictionary.Body != null && PdfValues.NameOf(_store.ResolveMaybe(dictionary.Get("Subtype"))) is not ("Form" or "Image"))
            {
                // Font programs and color profiles have no subtype, but remain opaque resources.
                if (PdfValues.NameOf(_store.ResolveMaybe(dictionary.Get("Subtype"))) is not (null or "Type1C" or "CIDFontType0C" or "OpenType"))
                {
                    return false;
                }
            }

            _bytes = checked(_bytes + 64 + (dictionary.Items.Count * 24L) + (dictionary.Body?.Length ?? 0));
            if (_bytes > MaxBytes)
            {
                return false;
            }

            foreach (var item in dictionary.Items)
            {
                if (!names && !ResourceKeys.Contains(item.Key))
                {
                    return false;
                }

                bool childNames = item.Key is "Font" or "XObject" or "ExtGState" or "ColorSpace" or "Pattern"
                    or "Shading" or "Properties" or "CharProcs";
                // /AP entries can be streams or state-name dictionaries.
                childNames |= depth == 0 && _store.Resolve(item.Value) is PdfObject.Dict { Body: null };
                if (!Inspect(item.Value, seen, depth + 1, childNames))
                {
                    return false;
                }
            }
        }
        else if (resolved is PdfObject.Arr array)
        {
            _bytes += 32 + (array.Items.Count * 8L);
            foreach (PdfObject item in array.Items)
            {
                if (!Inspect(item, seen, depth + 1))
                {
                    return false;
                }
            }
        }
        else
        {
            _bytes += 24 + (resolved is PdfObject.Str text ? text.Raw.Length : resolved is PdfObject.Name name ? name.Value.Length * 2L : 0);
        }

        return _bytes <= MaxBytes;
    }
}
