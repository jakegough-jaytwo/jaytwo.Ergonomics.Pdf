using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using jaytwo.Ergonomics.Pdf.AcroForm;
using jaytwo.Ergonomics.Pdf.Attachments;
using jaytwo.Ergonomics.Pdf.Internal;
using jaytwo.Ergonomics.Pdf.Metadata;
using jaytwo.Ergonomics.Pdf.Signatures;

namespace jaytwo.Ergonomics.Pdf.Fuzz;

/// <summary>
/// The target table. Keep it in step with <c>.agents/skills/pdf-fuzz-qualification/references/targets.md</c>.
/// </summary>
internal static class FuzzTargets
{
    public const int DocumentMaxLength = 1024 * 1024;

    public const int FragmentMaxLength = 64 * 1024;

    private const int DrainLimit = 1024 * 1024;

    private static readonly byte[] StreamKeyword = Encoding.ASCII.GetBytes("stream\n");

    private static readonly Func<byte[], byte[]> ExtractToken = CreateExtractToken();

    private static readonly Lazy<SeedSigner> Signer = new Lazy<SeedSigner>(SeedSigner.Create);

    public static IReadOnlyList<FuzzTarget> All { get; } = new[]
    {
        new FuzzTarget("open", DocumentMaxLength, Open),
        new FuzzTarget("edit", DocumentMaxLength, Edit),
        new FuzzTarget("object", FragmentMaxLength, ParseObject),
        new FuzzTarget("objstm", FragmentMaxLength, ParseObjectStream),
        new FuzzTarget("decode", FragmentMaxLength, Decode),
        new FuzzTarget("der", FragmentMaxLength, Der),
        new FuzzTarget("tsr", FragmentMaxLength, TimestampResponse),
        new FuzzTarget("xmp", FragmentMaxLength, Xmp),
    };

    public static FuzzTarget Find(string name)
    {
        foreach (FuzzTarget target in All)
        {
            if (string.Equals(target.Name, name, StringComparison.Ordinal))
            {
                return target;
            }
        }

        throw new ArgumentException("Unknown fuzz target '" + name + "'.", nameof(name));
    }

    /// <summary>
    /// Splits "&lt;dictionary&gt;stream\n&lt;data&gt;" input used by the decode target.
    /// </summary>
    public static (byte[] Head, byte[] Body) SplitAtStream(byte[] data)
    {
        int at = IndexOf(data, StreamKeyword);
        if (at < 0)
        {
            return (data, Array.Empty<byte>());
        }

        byte[] head = new byte[at];
        Buffer.BlockCopy(data, 0, head, 0, at);
        int bodyStart = at + StreamKeyword.Length;
        byte[] body = new byte[data.Length - bodyStart];
        Buffer.BlockCopy(data, bodyStart, body, 0, body.Length);
        return (head, body);
    }

    private static void Open(byte[] data)
    {
        try
        {
            using PdfDocument document = PdfDocument.Open(new MemoryStream(data, false), null);
            ReadAll(document);
        }
        catch (PdfException)
        {
        }
    }

    private static void Edit(byte[] data)
    {
        byte[] saved;
        try
        {
            using PdfDocument document = PdfDocument.Open(new MemoryStream(data, false), null);
            Mutate(document);
            var output = new MemoryStream();
            document.SaveIncremental(output, leaveOpen: true);
            saved = output.ToArray();
        }
        catch (PdfException)
        {
            return;
        }

        RequirePrefix(data, saved, "SaveIncremental");
        try
        {
            using PdfDocument reopened = PdfDocument.Open(new MemoryStream(saved, false), null);
            ReadAll(reopened);
            var signed = new MemoryStream();
            var options = new PdfSignatureOptions { Rect = new PdfRect(10, 10, 110, 40), Reason = "fuzz" };
            reopened.Sign(signed, options, new CmsDetachedSignatureProvider(Signer.Value), leaveOpen: true);
            RequirePrefix(saved, signed.ToArray(), "Sign");
        }
        catch (PdfException)
        {
        }
    }

    private static void ParseObject(byte[] data)
    {
        try
        {
            var parser = new PdfParser(PdfCursor.FromBytes(data), null, 0);
            parser.ParseValue();
        }
        catch (PdfException)
        {
        }
    }

    /// <summary>
    /// Input is "N First\n" followed by the decoded object-stream data. The guards mirror <c>PdfEngine.ParseObjectStream</c>,
    /// so the target sees only what the engine would pass.
    /// </summary>
    private static void ParseObjectStream(byte[] data)
    {
        int newline = Array.IndexOf(data, (byte)'\n');
        if (newline < 0 || newline > 32)
        {
            return;
        }

        string[] header = Encoding.ASCII.GetString(data, 0, newline).Split(' ');
        if (header.Length != 2
            || !int.TryParse(header[0], NumberStyles.None, CultureInfo.InvariantCulture, out int count)
            || !int.TryParse(header[1], NumberStyles.None, CultureInfo.InvariantCulture, out int first))
        {
            return;
        }

        byte[] body = new byte[data.Length - newline - 1];
        Buffer.BlockCopy(data, newline + 1, body, 0, body.Length);
        if (count > int.MaxValue / 2 || (count > 0 && ((long)count * 4) - 1 > first) || first > body.Length)
        {
            return;
        }

        try
        {
            PdfParser.ParseObjectStream(body, count, first);
        }
        catch (PdfException)
        {
        }
    }

    /// <summary>
    /// Input is a stream dictionary, "stream\n", then the encoded data. Both the buffered and the streaming decoders run.
    /// </summary>
    private static void Decode(byte[] data)
    {
        (byte[] head, byte[] body) = SplitAtStream(data);
        try
        {
            if (new PdfParser(PdfCursor.FromBytes(head), null, 0).ParseValue() is not PdfObject.Dict dict)
            {
                return;
            }

            List<string> filters = PdfStreamDecoder.FiltersOf(dict);
            List<PdfObject.Dict?> parms = PdfStreamDecoder.ParmsOf(dict, filters.Count);
            try
            {
                PdfStreamDecoder.Decode(body, filters, parms, PdfStreamDecoder.StructuralCap);
            }
            catch (PdfException)
            {
            }

            using Stream decoded = PdfStreamDecoder.OpenDecoded(new MemoryStream(body, false), filters, parms);
            Drain(decoded, PdfStreamDecoder.StructuralCap);
        }
        catch (PdfException)
        {
        }
    }

    private static void Der(byte[] data)
    {
        try
        {
            WalkDer(data, 0);
        }
        catch (PdfException)
        {
        }

        try
        {
            DerCodec.ReadCertificateIdentity(data);
        }
        catch (PdfException)
        {
        }
    }

    /// <summary>
    /// Input is a 32-byte SHA-256 imprint, a nonce length byte, the nonce, a mode byte, and the body.
    /// Mode 0 treats the body as a TimeStampResp; any other mode treats it as the token.
    /// </summary>
    private static void TimestampResponse(byte[] data)
    {
        if (data.Length < 34)
        {
            return;
        }

        int nonceLength = data[32];
        int modeAt = 33 + nonceLength;
        if (modeAt >= data.Length)
        {
            return;
        }

        var imprint = new ReadOnlySpan<byte>(data, 0, 32);
        var nonce = new ReadOnlySpan<byte>(data, 33, nonceLength);
        byte[] body = new byte[data.Length - modeAt - 1];
        Buffer.BlockCopy(data, modeAt + 1, body, 0, body.Length);
        try
        {
            byte[] token = data[modeAt] == 0 ? ExtractToken(body) : body;
            Rfc3161TimeStampToken.RequireMatch(token, imprint, nonce);
        }
        catch (PdfException)
        {
        }
    }

    /// <summary>
    /// Mirrors <c>PdfEngine.SyncXmp</c>: a packet that does not parse, or that <c>ApplyInfo</c> rejects as not XMP, is left
    /// alone; one that does is edited and serialized.
    /// </summary>
    private static void Xmp(byte[] data)
    {
        if (!XmpEditor.TryParse(data, out XDocument? document, out _))
        {
            return;
        }

        try
        {
            XmpEditor.ApplyInfo(document!, "Title", "fuzz");
            XmpEditor.ApplyInfo(document!, "Author", null);
            XmpEditor.ApplyInfo(document!, "CreationDate", "D:20261019120000Z");
        }
        catch (PdfXmpException)
        {
            return;
        }

        XmpEditor.ToBytes(document!);
    }

    private static void ReadAll(PdfDocument document)
    {
        _ = document.Permissions.ToString();
        _ = document.PageCount;
        foreach (PdfPage page in document.Pages)
        {
            _ = (page.Number, page.MediaBox, page.CropBox, page.Rotation);
        }

        PdfAcroForm form = document.AcroForm;
        if (form.HasFields)
        {
            _ = form.NeedAppearances;
            foreach (PdfField field in form.Fields)
            {
                _ = (field.PartialName, field.FullName, field.WidgetIndex, field.FieldType, field.ButtonKind, field.Value);
                _ = (field.SelectedValues.Count, field.ExportValue, field.IsChecked, field.Rect, field.PageNumber, field.PageRotation);
                _ = (field.MediaBox, field.ReadOnly, field.Required, field.Multiline, field.MaxLength, field.Editable, field.MultiSelect);
                foreach (PdfChoiceOption option in field.Options)
                {
                    _ = (option.Value, option.Display);
                }

                _ = form.GetFields(field.FullName).Count;
            }
        }

        PdfMetadata metadata = document.Metadata;
        _ = (metadata.Title, metadata.Author, metadata.Subject, metadata.Keywords, metadata.Creator, metadata.Producer);
        _ = (metadata.CreationDate, metadata.ModificationDate);
        foreach (string key in metadata.Keys)
        {
            _ = metadata[key];
        }

        if (metadata.HasXmp)
        {
            using Stream xmp = metadata.OpenXmp();
            Drain(xmp, DrainLimit);
        }

        foreach (PdfAttachment attachment in document.Attachments)
        {
            _ = (attachment.Name, attachment.Description, attachment.MimeType, attachment.Size);
            using Stream content = attachment.Open();
            Drain(content, DrainLimit);
        }
    }

    /// <summary>
    /// Applies one of each mutation. Each runs on its own so a refusal does not hide the later ones.
    /// </summary>
    private static void Mutate(PdfDocument document)
    {
        PdfAcroForm form = document.AcroForm;
        IReadOnlyList<PdfField> fields = Array.Empty<PdfField>();
        Try(() => fields = form.Fields);
        bool text = false;
        bool check = false;
        bool radio = false;
        bool choice = false;
        foreach (PdfField field in fields)
        {
            if (!text && field.FieldType == PdfFieldType.Text)
            {
                text = true;
                Try(() => form.SetText(field.FullName, "Fuzz é (1)"));
            }
            else if (!check && field.FieldType == PdfFieldType.Button && field.ButtonKind == PdfButtonKind.CheckBox)
            {
                check = true;
                Try(() => form.SetCheckBox(field.FullName, true));
            }
            else if (!radio && field.FieldType == PdfFieldType.Button && field.ButtonKind == PdfButtonKind.Radio && field.ExportValue != null)
            {
                radio = true;
                Try(() => form.SetRadio(field.FullName, field.ExportValue));
            }
            else if (!choice && field.FieldType == PdfFieldType.Choice && field.Options.Count > 0)
            {
                choice = true;
                Try(() => form.SetChoice(field.FullName, field.Options[field.Options.Count - 1].Value));
            }
        }

        Try(() => document.Metadata.Title = "fuzz");
        Try(() => document.Metadata.SetXmp(Encoding.UTF8.GetBytes("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/>")));
        Try(() => document.Attachments.Add("fuzz.txt", new MemoryStream(Encoding.ASCII.GetBytes("fuzz"))));
        if (document.Attachments.Count > 1)
        {
            Try(() => document.Attachments.Remove(document.Attachments[0].Name));
        }
    }

    private static void Try(Action action)
    {
        try
        {
            action();
        }
        catch (PdfException)
        {
        }
    }

    private static void RequirePrefix(byte[] original, byte[] output, string operation)
    {
        if (output.Length < original.Length || !new ReadOnlySpan<byte>(output, 0, original.Length).SequenceEqual(original))
        {
            throw new InvalidOperationException(operation + " output does not keep the original bytes as a prefix.");
        }
    }

    private static void WalkDer(byte[] data, int depth)
    {
        int offset = 0;
        while (offset < data.Length)
        {
            DerCodec.DerTlv tlv = DerCodec.ReadTlv(data, offset);
            if (tlv.Tag == 0x02)
            {
                try
                {
                    DerCodec.ReadInteger(tlv);
                }
                catch (PdfException)
                {
                }
            }

            if ((tlv.Tag & 0x20) != 0 && depth < 64)
            {
                WalkDer(tlv.Content, depth + 1);
            }

            offset += tlv.Full.Length;
        }
    }

    private static void Drain(Stream stream, int limit)
    {
        byte[] buffer = new byte[8192];
        long total = 0;
        int read;
        while (total <= limit && (read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;
        }
    }

    private static int IndexOf(byte[] data, byte[] pattern)
    {
        return new ReadOnlySpan<byte>(data).IndexOf(pattern);
    }

    private static Func<byte[], byte[]> CreateExtractToken()
    {
        MethodInfo method = typeof(HttpTimestampAuthority).GetMethod("ExtractToken", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("HttpTimestampAuthority.ExtractToken was not found.");
        return (Func<byte[], byte[]>)method.CreateDelegate(typeof(Func<byte[], byte[]>));
    }
}
