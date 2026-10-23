using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using iText.Forms.Fields;
using iText.Kernel.Pdf;
using iText.Signatures;
using Xunit;
using ITextAcroForm = iText.Forms.PdfAcroForm;
using ITextDocument = iText.Kernel.Pdf.PdfDocument;

namespace jaytwo.Ergonomics.Pdf.Tests;

// W-112: iText Core as an independent reader. A read fails when iText had to rebuild the cross-reference table: that is
// iText recovering from a file it could not parse, by a line scan that loses objects in object streams.
internal static class IText
{
    public const string Version = "9.8.0";

    public static ITextReading Read(byte[] pdf, string? password = null)
    {
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

    // W-117: iText reads an incremental update only after a classic cross-reference table. This is true when the
    // section at the file's last startxref is one, as it is on a classic or hybrid file.
    public static bool ReadsAppendsTo(byte[] pdf)
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

        foreach (KeyValuePair<string, PdfFormField> pair in form.GetAllFormFields())
        {
            if (pair.Value.GetChildFormFields().Count == 0)
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
