using System.Collections.Generic;

namespace jaytwo.Pdf.Tests;

// What iText read from one file. See IText.
internal sealed class ITextReading
{
    public ITextReading(int pages, string? title, string? xmp, IReadOnlyList<int> rotations, IReadOnlyDictionary<string, string?> fields, IReadOnlyDictionary<string, byte[]> attachments, IReadOnlyDictionary<string, ITextSignature> signatures)
    {
        Pages = pages;
        Title = title;
        Xmp = xmp;
        Rotations = rotations;
        Fields = fields;
        Attachments = attachments;
        Signatures = signatures;
    }

    public int Pages { get; }

    // The trailer's Info /Title as iText parses it. PdfDocumentInfo.GetTitle returns the XMP dc:title instead when
    // the file has XMP metadata.
    public string? Title { get; }

    // The catalog /Metadata stream as UTF-8 text, or null when there is none.
    public string? Xmp { get; }

    public IReadOnlyList<int> Rotations { get; }

    // Every terminal field by full name, with iText's text value.
    public IReadOnlyDictionary<string, string?> Fields { get; }

    // Each embedded file by name-tree key, decoded.
    public IReadOnlyDictionary<string, byte[]> Attachments { get; }

    // Each signed signature field by name.
    public IReadOnlyDictionary<string, ITextSignature> Signatures { get; }
}
