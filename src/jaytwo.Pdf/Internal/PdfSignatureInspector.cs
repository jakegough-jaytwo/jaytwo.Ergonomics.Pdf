#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Text;
using jaytwo.Pdf.AcroForm;
using jaytwo.Pdf.Signatures;

namespace jaytwo.Pdf.Internal;

// Lists signatures and revisions. It reads the field tree, the signature dictionaries, and the revision footers.
internal sealed class PdfSignatureInspector
{
    private const int MaxRevisionFooterBytes = 64 * 1024;

    private readonly PdfObjectStore _store;
    private readonly PdfWriteGate _gate;
    private readonly PdfFieldTree _fields;

    internal PdfSignatureInspector(PdfObjectStore store, PdfWriteGate gate, PdfFieldTree fields)
    {
        _store = store;
        _gate = gate;
        _fields = fields;
    }

    // Spec R9. One entry per terminal signature field, from the value owner each widget names, so a field with two
    // widgets is listed once. Only the field tree, the signature dictionaries, and the xref state are read.
    internal IReadOnlyList<PdfSignatureInfo> ListSignatures()
    {
        _store.CheckDisposed();
        using var release = _store.ReleaseAfter();
        IReadOnlyDictionary<long, int> revisionNumbers;
        try
        {
            revisionNumbers = RevisionNumbersByEnd();
        }
        catch (PdfParseException)
        {
            // Signature inspection remains available when a historical revision footer is malformed.
            revisionNumbers = new Dictionary<long, int>();
        }

        var list = new List<PdfSignatureInfo>();
        var seen = new HashSet<int>();
        foreach (FieldSlot slot in _fields.CollectFields())
        {
            if (slot.FieldType != PdfFieldType.Signature || !seen.Add(slot.ValueObjectId))
            {
                continue;
            }

            PdfObject? value = _store.Load(slot.ValueObjectId) is PdfObject.Dict field ? field.Get("V") : null;
            if (_store.ResolveMaybe(value) is PdfObject.Dict signature)
            {
                int id = value is PdfObject.Ref reference ? reference.ObjectId : slot.ValueObjectId;
                long offset = _store.Xref.TryGetValue(id, out XrefEntry entry) && entry.Type == 1 ? entry.Field1 : 0;
                list.Add(ReadSignature(slot.FullName, signature, offset, revisionNumbers));
            }
            else
            {
                list.Add(new PdfSignatureInfo(slot.FullName, false, null, null, Array.Empty<byte[]>(), null, null, null, null, null, null, null, null, null, PdfByteRangeStatus.Missing, null, false, null, null));
            }
        }

        return list;
    }

    // Spec R10. The xref chain is stored newest first; reversing it gives chronological order. In a linearized file,
    // the main section is followed by the lower-offset first-page section, and those two sections form one revision.
    internal IReadOnlyList<PdfRevision> ListRevisions()
    {
        _store.CheckDisposed();
        using var release = _store.ReleaseAfter();
        List<long> ends = LogicalRevisionEnds();
        HashSet<long> signedEnds = SignatureRevisionEnds();
        var revisions = new List<PdfRevision>(ends.Count);
        for (int i = 0; i < ends.Count; i++)
        {
            revisions.Add(new PdfRevision(i + 1, ends[i], signedEnds.Contains(ends[i])));
        }

        return revisions;
    }

    private IReadOnlyDictionary<long, int> RevisionNumbersByEnd()
    {
        List<long> ends = LogicalRevisionEnds();
        var numbers = new Dictionary<long, int>();
        for (int i = 0; i < ends.Count; i++)
        {
            numbers[ends[i]] = i + 1;
        }

        return numbers;
    }

    private List<long> LogicalRevisionEnds()
    {
        var sections = new List<RevisionSection>(_store.RevisionSections);
        sections.Reverse();
        var ends = new List<long>(sections.Count);
        bool linearized = sections.Count > 1 && sections[1].Offset < sections[0].Offset;
        for (int i = 0; i < sections.Count; i++)
        {
            // W-170: a footer names the section it follows. In a linearized file the main section's footer names the
            // first-page section, and the first-page section's own startxref is not checked (ISO 32000-1 F.3).
            bool linearizedFirstPageSection = linearized && i == 1;
            long? named = linearized && i == 0 ? sections[1].Offset : linearizedFirstPageSection ? null : sections[i].Offset;
            long end = RevisionEnd(sections[i].EndOfSection, named);
            if (linearizedFirstPageSection)
            {
                ends[0] = Math.Max(ends[0], end);
            }
            else
            {
                ends.Add(end);
            }
        }

        return ends;
    }

    private HashSet<long> SignatureRevisionEnds()
    {
        var ends = new HashSet<long>();
        var seen = new HashSet<int>();
        foreach (FieldSlot slot in _fields.CollectFields())
        {
            if (slot.FieldType != PdfFieldType.Signature || !seen.Add(slot.ValueObjectId))
            {
                continue;
            }

            PdfObject? value = _store.Load(slot.ValueObjectId) is PdfObject.Dict field ? field.Get("V") : null;
            if (_store.ResolveMaybe(value) is not PdfObject.Dict signature)
            {
                continue;
            }

            int id = value is PdfObject.Ref reference ? reference.ObjectId : slot.ValueObjectId;
            long offset = _store.Xref.TryGetValue(id, out XrefEntry entry) && entry.Type == 1 ? entry.Field1 : 0;
            if (ReadByteRange(signature, offset) is PdfByteRange range)
            {
                ends.Add(range.Offset1 + range.Length1);
            }
        }

        return ends;
    }

    private long RevisionEnd(long sectionEnd, long? namedSection)
    {
        if (sectionEnd < 0 || sectionEnd >= _store.Length)
        {
            throw new PdfParseException(sectionEnd, "The cross-reference section has no revision footer.");
        }

        int count = (int)Math.Min(MaxRevisionFooterBytes, _store.Length - sectionEnd);
        byte[] footer = _store.ReadRange(sectionEnd, count);
        byte[] startXref = Encoding.ASCII.GetBytes("startxref");

        // ISO 32000-1 7.5.5 and 7.5.8.1: the section's own footer follows it, after white space and comments (7.2.3).
        // A footer found further on belongs to a later revision, so a section without one does not borrow it (W-150),
        // and a footer that names another section is not this one's (W-170).
        int marker = SkipWhiteSpaceAndComments(footer, 0);
        if (!StartsWith(footer, startXref, marker) || !PdfDocumentLoader.TryParseStartXref(footer, marker + startXref.Length, out long named))
        {
            throw new PdfParseException(sectionEnd, "The cross-reference section is not followed by its own startxref / %%EOF footer.");
        }

        if (namedSection is long expected && named != expected)
        {
            throw new PdfParseException(sectionEnd, "The footer after the cross-reference section names another section.");
        }

        byte[] eof = Encoding.ASCII.GetBytes("%%EOF");
        int eofIndex = IndexOf(footer, eof, marker + startXref.Length);
        int end = eofIndex + eof.Length;
        while (end < footer.Length && footer[end] is 0 or 9 or 10 or 12 or 13 or 32)
        {
            end++;
        }

        if (end == footer.Length && sectionEnd + end < _store.Length)
        {
            throw new PdfParseException(sectionEnd, "The cross-reference section's revision footer exceeds 64 KiB.");
        }

        return sectionEnd + end;
    }

    private PdfSignatureInfo ReadSignature(string fieldName, PdfObject.Dict signature, long offset, IReadOnlyDictionary<long, int> revisionNumbers)
    {
        string? subFilter = PdfValues.NameOf(_store.ResolveMaybe(signature.Get("SubFilter")));
        PdfByteRange? byteRange = ReadByteRange(signature, offset);

        CmsSignatureReader.Result? cms = null;
        if (subFilter is "adbe.pkcs7.detached" or "adbe.pkcs7.sha1" or "ETSI.CAdES.detached" or "ETSI.RFC3161")
        {
            if (_store.ResolveMaybe(signature.Get("Contents")) is not PdfObject.Str contents)
            {
                throw new PdfParseException(offset, "The signature has no /Contents string.");
            }

            cms = CmsSignatureReader.Read(contents.Raw, subFilter == "ETSI.RFC3161", offset);
        }

        string? modified = SignatureText(signature.Get("M"));
        int certification = _gate.PermissionOnSignature(signature, "DocMDP");
        return new PdfSignatureInfo(
            fieldName,
            true,
            subFilter,
            cms?.SignerCertificate,
            cms?.Certificates ?? Array.Empty<byte[]>(),
            cms?.DigestAlgorithm,
            cms?.SignatureAlgorithm,
            modified == null ? null : PdfTextCodec.ParseDate(modified),
            cms?.SigningTime,
            SignatureText(signature.Get("Name")),
            SignatureText(signature.Get("Reason")),
            SignatureText(signature.Get("Location")),
            SignatureText(signature.Get("ContactInfo")),
            byteRange,
            ByteRangeStatusOf(byteRange, offset),
            byteRange is PdfByteRange numbered && revisionNumbers.TryGetValue(numbered.Offset1 + numbered.Length1, out int revisionNumber) ? revisionNumber : null,
            byteRange is PdfByteRange signed && _store.StartXref >= signed.Offset1 + signed.Length1,
            cms?.TimestampTime,
            certification == 0 ? null : certification);
    }

    private PdfByteRange? ReadByteRange(PdfObject.Dict signature, long offset)
    {
        PdfObject? rangeValue = _store.ResolveMaybe(signature.Get("ByteRange"));
        if (rangeValue == null)
        {
            return null;
        }

        var numbers = new long[4];
        if (rangeValue is not PdfObject.Arr range || range.Items.Count != 4)
        {
            throw new PdfParseException(offset, "The signature /ByteRange is not four integers.");
        }

        for (int i = 0; i < 4; i++)
        {
            if (_store.ResolveMaybe(range.Items[i]) is not PdfObject.Num { IsInteger: true, LongValue: >= 0 } number)
            {
                throw new PdfParseException(offset, "The signature /ByteRange is not four non-negative integers.");
            }

            numbers[i] = number.LongValue;
        }

        if (numbers[2] > long.MaxValue - numbers[3])
        {
            throw new PdfParseException(offset, "The signature /ByteRange ends past the largest offset.");
        }

        return new PdfByteRange(numbers[0], numbers[1], numbers[2], numbers[3]);
    }

    // W-150 and W-170, ISO 32000-1 12.8.1: the first range starts the file, neither range is empty, both lie inside the
    // file in order, and the gap between them is exactly the signature dictionary's own /Contents hex string as written.
    // The checks follow the order of PdfByteRangeStatus. Only the signature dictionary is read again, never the signed bytes.
    private PdfByteRangeStatus ByteRangeStatusOf(PdfByteRange? byteRange, long signatureOffset)
    {
        if (byteRange is not PdfByteRange range)
        {
            return PdfByteRangeStatus.Missing;
        }

        if (range.Offset0 != 0)
        {
            return PdfByteRangeStatus.NotFromFileStart;
        }

        if (range.Length0 == 0 || range.Length1 == 0)
        {
            return PdfByteRangeStatus.EmptyRange;
        }

        long gapStart = range.Length0;
        if (gapStart > _store.Length || range.Offset1 + range.Length1 > _store.Length)
        {
            return PdfByteRangeStatus.PastEndOfFile;
        }

        if (range.Offset1 - gapStart < 2)
        {
            return PdfByteRangeStatus.Overlapping;
        }

        if (ContentsSpan(signatureOffset) is not (long start, long end) || start != gapStart || end != range.Offset1)
        {
            return PdfByteRangeStatus.GapNotContents;
        }

        return PdfByteRangeStatus.WellFormed;
    }

    // The source offsets where the uncompressed signature dictionary's /Contents hex string starts and ends, read
    // lexically from the file. Null when the dictionary is in an object stream (offset 0) or /Contents is not a hex
    // string written directly in it.
    private (long Start, long End)? ContentsSpan(long signatureOffset)
    {
        if (signatureOffset <= 0)
        {
            return null;
        }

        PdfCursor cursor = _store.Cursor;
        cursor.Seek(signatureOffset);
        var parser = new PdfParser(cursor, null, 0);
        PdfLexer lexer = parser.Lexer;
        if (PdfSync.Result(lexer.NextAsync()).Kind != PdfTokenKind.Integer
            || PdfSync.Result(lexer.NextAsync()).Kind != PdfTokenKind.Integer
            || PdfSync.Result(lexer.NextAsync()) is not { Kind: PdfTokenKind.Keyword, Text: "obj" }
            || PdfSync.Result(lexer.NextAsync()).Kind != PdfTokenKind.DictStart)
        {
            return null;
        }

        while (true)
        {
            PdfToken key = PdfSync.Result(lexer.NextAsync());
            if (key.Kind != PdfTokenKind.Name)
            {
                return null;
            }

            SkipWhiteSpaceAndComments(lexer);
            long start = cursor.Position;
            bool hex = PdfSync.Result(lexer.PeekByteAsync()) == '<';
            PdfObject value = parser.ParseValue();
            if (key.Text == "Contents")
            {
                return hex && value is PdfObject.Str ? (start, cursor.Position) : null;
            }
        }
    }

    private static void SkipWhiteSpaceAndComments(PdfLexer lexer)
    {
        while (true)
        {
            int b = PdfSync.Result(lexer.PeekByteAsync());
            if (b == '%')
            {
                while (b >= 0 && b is not (10 or 13))
                {
                    PdfSync.Result(lexer.ReadByteAsync());
                    b = PdfSync.Result(lexer.PeekByteAsync());
                }
            }
            else if (b >= 0 && PdfLexer.IsWhitespace(b))
            {
                PdfSync.Result(lexer.ReadByteAsync());
            }
            else
            {
                return;
            }
        }
    }

    private string? SignatureText(PdfObject? value)
    {
        return _store.ResolveMaybe(value) is PdfObject.Str text ? PdfTextCodec.Decode(text.Raw) : null;
    }

    // A comment runs from % to the end of the line, and its line end is white space.
    private static int SkipWhiteSpaceAndComments(byte[] data, int index)
    {
        while (index < data.Length)
        {
            if (PdfLexer.IsWhitespace(data[index]))
            {
                index++;
            }
            else if (data[index] == (byte)'%')
            {
                while (index < data.Length && data[index] is not (10 or 13))
                {
                    index++;
                }
            }
            else
            {
                break;
            }
        }

        return index;
    }

    private static bool StartsWith(byte[] data, byte[] needle, int start)
    {
        if (start + needle.Length > data.Length)
        {
            return false;
        }

        for (int i = 0; i < needle.Length; i++)
        {
            if (data[start + i] != needle[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int IndexOf(byte[] data, byte[] needle, int start)
    {
        for (int i = Math.Max(0, start); i + needle.Length <= data.Length; i++)
        {
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
}
