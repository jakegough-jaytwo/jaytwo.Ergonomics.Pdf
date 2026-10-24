using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using iText.Forms.Fields;
using iText.Kernel.Pdf;
using iText.Signatures;
using Xunit;
using ITextAcroForm = iText.Forms.PdfAcroForm;
using ITextDocument = iText.Kernel.Pdf.PdfDocument;

namespace jaytwo.Pdf.Tests;

// W-112: iText Core as an independent reader. A read fails when iText had to rebuild the cross-reference table: that is
// iText recovering from a file it could not parse, by a line scan that loses objects in object streams.
internal static class IText
{
    public const string Version = "9.8.0";

    public static ITextReading Read(byte[] pdf, string? password = null)
    {
        Lane.Refuse("iText", "[Trait(Lane.Trait, Lane.Interop)]");
        var properties = new ReaderProperties();
        if (password != null)
        {
            properties.SetPassword(Encoding.UTF8.GetBytes(password));
        }

        using var source = new MemoryStream(pdf, writable: false);
        using var reader = new PdfReader(source, properties);
        using var document = new ITextDocument(reader);
        Assert.False(reader.HasRebuiltXref(), "iText " + Version + " rebuilt the cross-reference table");
        var rotations = new List<int>();
        for (int page = 1; page <= document.GetNumberOfPages(); page++)
        {
            rotations.Add(document.GetPage(page).GetRotation());
        }

        byte[]? xmp = document.GetXmpMetadataBytes();
        return new ITextReading(
            document.GetNumberOfPages(),
            document.GetTrailer().GetAsDictionary(PdfName.Info)?.GetAsString(PdfName.Title)?.ToUnicodeString(),
            xmp == null ? null : Encoding.UTF8.GetString(xmp),
            rotations,
            Fields(document),
            Attachments(document),
            Signatures(document));
    }

    // True when the section at the file's last startxref is a classic table, as on a classic or hybrid file. iText reads
    // an update only in the format of the section before it, so the library matches it (W-117, D-56).
    public static bool EndsInClassicXref(byte[] pdf)
    {
        int tail = Math.Max(0, pdf.Length - 1024);
        string end = TestCompat.Latin1.GetString(pdf, tail, pdf.Length - tail);
        MatchCollection matches = Regex.Matches(end, @"startxref\s+(\d+)");
        Assert.True(matches.Count > 0, "no startxref in the last 1024 bytes");
        int offset = int.Parse(matches[matches.Count - 1].Groups[1].Value, CultureInfo.InvariantCulture);
        string section = TestCompat.Latin1.GetString(pdf, offset, Math.Min(16, pdf.Length - offset));
        return section.TrimStart().StartsWith("xref", StringComparison.Ordinal);
    }

    private static Dictionary<string, string?> Fields(ITextDocument document)
    {
        var fields = new Dictionary<string, string?>(StringComparer.Ordinal);
        ITextAcroForm? form = ITextAcroForm.GetAcroForm(document, false);
        if (form == null)
        {
            return fields;
        }

        // A field is terminal when none of its kids has a /T, the rule form_values.py follows (ISO 32000-1 12.7.3.1).
        // iText counts a kid without /T as a child field when it carries a field key such as /DA, as some widgets on the
        // USCIS I-9 and FAA 8710-1 do, and lists no name for it.
        foreach (KeyValuePair<string, PdfFormField> pair in form.GetAllFormFields())
        {
            if (pair.Value.GetChildFormFields().All(child => string.IsNullOrEmpty(child.GetPartialFieldName()?.ToUnicodeString())))
            {
                fields[pair.Key] = pair.Value.GetValueAsString();
            }
        }

        return fields;
    }

    private static Dictionary<string, byte[]> Attachments(ITextDocument document)
    {
        var attachments = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (document.GetCatalog().GetPdfObject().GetAsDictionary(PdfName.Names)?.Get(PdfName.EmbeddedFiles) == null)
        {
            return attachments;
        }

        foreach (KeyValuePair<PdfString, PdfObject> entry in document.GetCatalog().GetNameTree(PdfName.EmbeddedFiles).GetNames())
        {
            PdfObject value = entry.Value.IsIndirectReference() ? ((PdfIndirectReference)entry.Value).GetRefersTo() : entry.Value;
            PdfStream? file = (value as PdfDictionary)?.GetAsDictionary(PdfName.EF)?.GetAsStream(PdfName.F);
            if (file != null)
            {
                attachments[entry.Key.ToUnicodeString()] = file.GetBytes();
            }
        }

        return attachments;
    }

    private static Dictionary<string, ITextSignature> Signatures(ITextDocument document)
    {
        var signatures = new Dictionary<string, ITextSignature>(StringComparer.Ordinal);
        var util = new SignatureUtil(document);
        foreach (string name in util.GetSignatureNames())
        {
            PdfPKCS7 data = util.ReadSignatureData(name);
            signatures[name] = new ITextSignature(data.VerifySignatureIntegrityAndAuthenticity(), util.SignatureCoversWholeDocument(name), data.GetReason());
        }

        return signatures;
    }
}
