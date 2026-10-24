using System.Collections.Generic;

namespace jaytwo.Pdf.Internal;

// W-115: names and keywords that recur in nearly every file. A parsed token that matches one reuses its string, so
// parsing a form allocates no string for its keys. The table is fixed when the type loads, so input cannot grow it.
// Every entry is ASCII, whose UTF-8 and byte-per-character readings agree, so a match is the string the parser would
// otherwise have built for a name or a keyword.
internal static class PdfCommonTokens
{
    private const int TableMask = 1023;

    // Chosen from the G7 form and the corpus forms (W-115 Notes), and from the structures the library reads. It is
    // declared before the table, which is built from it when the type loads.
    private static readonly string[] EntryList =
    {
        // Keywords.
        "R", "obj", "endobj", "stream", "endstream", "true", "false", "null", "xref", "trailer", "startxref", "n", "f",

        // File structure, catalog, and document information.
        string.Empty, "Type", "Subtype", "Length", "Filter", "DecodeParms", "FlateDecode", "Fl", "LZWDecode", "ASCIIHexDecode",
        "ASCII85Decode", "RunLengthDecode", "DCTDecode", "Predictor", "Columns", "Colors", "BitsPerComponent", "ObjStm",
        "XRef", "N", "First", "Extends", "Size", "Index", "Prev", "W", "XRefStm", "Root", "Info", "ID", "Encrypt",
        "Catalog", "Version", "Metadata", "XML", "Names", "Dests", "Outlines", "OpenAction", "PageMode", "PageLayout",
        "ViewerPreferences", "Lang", "MarkInfo", "Marked", "StructTreeRoot", "AcroForm", "Perms", "DocMDP", "UR3",
        "Title", "Author", "Subject", "Keywords", "Creator", "Producer", "CreationDate", "ModDate", "Trapped",
        "EmbeddedFiles", "EmbeddedFile", "Filespec", "UF", "EF", "Desc", "AF", "AFRelationship", "Params", "CheckSum",
        "Kids", "Limits", "Nums",

        // Pages and resources.
        "Pages", "Page", "Parent", "Count", "MediaBox", "CropBox", "BleedBox", "TrimBox", "ArtBox", "Rotate", "Resources",
        "Contents", "Annots", "Group", "Tabs", "StructParents", "StructParent", "Font", "XObject", "ExtGState",
        "ColorSpace", "Pattern", "Shading", "ProcSet", "Properties", "PDF", "Text", "ImageB", "ImageC", "ImageI", "Image",
        "Form", "FormType", "BBox", "Matrix", "Transparency", "CS", "DeviceRGB", "DeviceGray", "DeviceCMYK", "Width",
        "Height", "Decode",

        // Fonts.
        "BaseFont", "Encoding", "WinAnsiEncoding", "PDFDocEncoding", "StandardEncoding", "MacRomanEncoding",
        "Type0", "Type1", "TrueType", "CIDFontType0", "CIDFontType2", "FontDescriptor", "FontName", "FontFamily",
        "FontStretch", "FontWeight", "Flags", "FontBBox", "ItalicAngle", "Ascent", "Descent", "CapHeight", "XHeight",
        "StemV", "StemH", "FontFile", "FontFile2", "FontFile3", "Length1", "Length2", "Length3", "FirstChar", "LastChar",
        "Widths", "ToUnicode", "DescendantFonts", "CIDSystemInfo", "Registry", "Ordering", "Supplement", "CIDToGIDMap",
        "Identity", "Identity-H", "DW", "Helv", "HeBo", "ZaDb", "Helvetica", "ZapfDingbats", "Normal",

        // Annotations, fields, and appearances.
        "Annot", "Widget", "Link", "Popup", "Rect", "P", "F", "M", "NM", "T", "TU", "TM", "FT", "Ff", "V", "DV", "DA", "DR",
        "Q", "MaxLen", "Opt", "TI", "I", "AP", "AS", "D", "MK", "BS", "BC", "BG", "CA", "RC", "AC", "S", "Border", "Fields",
        "NeedAppearances", "SigFlags", "CO", "XFA", "Tx", "Btn", "Ch", "Sig", "Off", "On", "Yes", "No", "A", "AA", "JS",
        "JavaScript", "URI", "GoTo", "Dest", "XYZ", "Lock", "SV",

        // Signatures.
        "ByteRange", "Reference", "TransformMethod", "TransformParams", "FieldMDP", "Action", "Location", "Reason",
        "ContactInfo", "Prop_Build", "SubFilter", "Cert", "DocTimeStamp", "adbe.pkcs7.detached", "ETSI.CAdES.detached",
        "ETSI.RFC3161", "Adobe.PPKLite", "DSS",

        // Encryption.
        "Standard", "O", "U", "OE", "UE", "EncryptMetadata", "CF", "StmF", "StrF", "StdCF", "CFM", "AESV2", "AESV3", "V2",
        "AuthEvent", "DocOpen",

        // Logical structure.
        "StructElem", "K", "Pg", "Obj", "OBJR", "MCR", "MCID", "ParentTree", "RoleMap", "ClassMap", "Alt", "ActualText",
        "Div", "Span", "Table", "TR", "TH", "TD", "TBody", "Sect", "LI", "LBody", "Lbl", "L", "H1", "H2", "H3", "Figure",
        "Artifact", "Layout", "Placement", "Block", "Document", "Part", "NonStruct", "ColSpan", "RowSpan", "Headers",
    };

    private static readonly string?[] Table = Build();

    internal static IReadOnlyList<string> Entries => EntryList;

    // The string for the first length bytes of buffer when it is an entry, otherwise null.
    internal static string? Find(byte[] buffer, int length)
    {
        uint hash = 2166136261;
        for (int i = 0; i < length; i++)
        {
            hash = (hash ^ buffer[i]) * 16777619;
        }

        int slot = (int)(hash & TableMask);
        while (true)
        {
            string? entry = Table[slot];
            if (entry == null)
            {
                return null;
            }

            if (Matches(entry, buffer, length))
            {
                return entry;
            }

            slot = (slot + 1) & TableMask;
        }
    }

    private static bool Matches(string entry, byte[] buffer, int length)
    {
        if (entry.Length != length)
        {
            return false;
        }

        for (int i = 0; i < length; i++)
        {
            if (entry[i] != buffer[i])
            {
                return false;
            }
        }

        return true;
    }

    private static string?[] Build()
    {
        var table = new string?[TableMask + 1];
        foreach (string entry in EntryList)
        {
            uint hash = 2166136261;
            foreach (char c in entry)
            {
                hash = (hash ^ c) * 16777619;
            }

            int slot = (int)(hash & TableMask);
            while (table[slot] != null && table[slot] != entry)
            {
                slot = (slot + 1) & TableMask;
            }

            table[slot] = entry;
        }

        return table;
    }
}
