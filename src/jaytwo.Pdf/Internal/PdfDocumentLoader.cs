#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Internal;

// Opens a document into its object store: the header, startxref, the cross-reference chain and its trailers, and the
// standard security handler.
internal sealed class PdfDocumentLoader
{
    private readonly PdfObjectStore _store;

    internal PdfDocumentLoader(PdfObjectStore store)
    {
        _store = store;
    }

    internal async ValueTask InitializeAsync(string? password, bool useAsync, CancellationToken cancellationToken)
    {
        if (_store.Length < 8)
        {
            throw new PdfParseException(0, "The stream is too small to be a PDF.");
        }

        await EnsureHeaderAsync(useAsync, cancellationToken).ConfigureAwait(false);
        _store.StartXref = await FindStartXrefAsync(useAsync, cancellationToken).ConfigureAwait(false);
        await LoadXrefAsync(_store.StartXref, true, true, true, new HashSet<long>(), password, useAsync, cancellationToken).ConfigureAwait(false);
        if (_store.RootId == 0)
        {
            throw new PdfParseException(_store.StartXref, "The trailer has no /Root.");
        }

        foreach (int key in _store.Xref.Keys)
        {
            if (key >= _store.Size && key < int.MaxValue)
            {
                _store.Size = key + 1;
            }
        }
    }

    private async ValueTask EnsureHeaderAsync(bool useAsync, CancellationToken cancellationToken)
    {
        int count = (int)Math.Min(1024, _store.Length);
        byte[] head = await _store.ReadRangeAsync(0, count, useAsync, cancellationToken).ConfigureAwait(false);
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
                int minor = head[i + 7] >= (byte)'0' && head[i + 7] <= (byte)'9' ? head[i + 7] - '0' : 0;
                _store.HeaderVersion = ((head[i + 5] - '0') * 10) + minor;
                return;
            }
        }

        throw new PdfParseException(0, "A %PDF-1. or %PDF-2. header was not found in the first 1024 bytes.");
    }

    private async ValueTask<long> FindStartXrefAsync(bool useAsync, CancellationToken cancellationToken)
    {
        foreach (long window in new long[] { 65536, 1024 * 1024 })
        {
            long size = Math.Min(window, _store.Length);
            long start = _store.Length - size;
            byte[] tail = await _store.ReadRangeAsync(start, (int)size, useAsync, cancellationToken).ConfigureAwait(false);
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

                if (value < 0 || value >= _store.Length)
                {
                    throw new PdfParseException(start + index, "startxref points past EOF.");
                }

                return value;
            }
        }

        throw new PdfParseException(_store.Length, "startxref was not found.");
    }

    private async ValueTask LoadXrefAsync(long offset, bool overwrite, bool recordTrailer, bool recordRevision, HashSet<long> stack, string? password, bool useAsync, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!stack.Add(offset))
        {
            throw new PdfParseException(offset, "Cyclic /Prev chain.");
        }

        if (stack.Count > PdfObjectStore.MaxTraversalDepth)
        {
            throw new PdfParseException(offset, "The cross-reference chain is too deep.");
        }

        if (_store.LoadedXrefs.Contains(offset))
        {
            stack.Remove(offset);
            return;
        }

        bool classic = await IsClassicXrefAsync(offset, useAsync, cancellationToken).ConfigureAwait(false);
        _store.Cursor.Seek(offset);
        var parser = new PdfParser(_store.Cursor, _store.ResolveAsync, 0, useAsync, cancellationToken);
        int budget = PdfXrefReader.EntryBudget(_store.Length);
        PdfObject.Dict trailer;
        long sectionEnd;
        bool pendingEncrypt;
        if (classic)
        {
            // The table is merged as it is read, before its /XRefStm stream and before /Prev, the search order of
            // ISO 32000-1 7.5.8.4. A /Prev chain never holds a second copy of the map.
            trailer = await PdfXrefReader.ParseClassicAsync(parser, _store.Xref, overwrite, budget).ConfigureAwait(false);
            sectionEnd = _store.Cursor.Position;
            pendingEncrypt = !await TryEnsureSecurityAsync(trailer, _store.Xref, password, useAsync, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            PdfObject obj = await parser.ParseIndirectAsync(-1).ConfigureAwait(false);
            if (obj is not PdfObject.Dict streamDict || streamDict.Body == null)
            {
                throw new PdfParseException(offset, "Xref stream object was expected.");
            }

            sectionEnd = _store.Cursor.Position;
            // A stream section is merged as it is read, so a /Prev chain never holds a second copy of the map.
            await ReadXrefStreamAsync(streamDict, overwrite, budget, useAsync, cancellationToken).ConfigureAwait(false);
            pendingEncrypt = !await TryEnsureSecurityAsync(streamDict, _store.Xref, password, useAsync, cancellationToken).ConfigureAwait(false);
            trailer = streamDict;
        }

        if (classic && PdfValues.LongOrNull(trailer.Get("XRefStm")) is long streamOffset)
        {
            await LoadXrefAsync(streamOffset, false, false, false, stack, password, useAsync, cancellationToken).ConfigureAwait(false);
        }

        long? previous = PdfValues.LongOrNull(trailer.Get("Prev"));
        if (recordRevision)
        {
            _store.RevisionSections.Add(new RevisionSection(offset, sectionEnd));
        }

        PdfObject.Dict? encryptedInline = null;
        if (recordTrailer)
        {
            TakeTrailer(trailer);
            if (_store.InlineInfo != null && _store.Security == null)
            {
                encryptedInline = _store.InlineInfo;
            }
        }

        _store.LoadedXrefs.Add(offset);
        if (previous is long previousOffset)
        {
            await LoadXrefAsync(previousOffset, false, _store.RootId == 0, true, stack, password, useAsync, cancellationToken).ConfigureAwait(false);
        }

        bool chainComplete = stack.Count == 1;
        if (pendingEncrypt && !await TryEnsureSecurityAsync(trailer, _store.Xref, password, useAsync, cancellationToken).ConfigureAwait(false) && chainComplete)
        {
            throw new PdfParseException(offset, "The encryption dictionary is not an uncompressed object in the cross-reference table.");
        }

        if (encryptedInline != null && _store.Security != null)
        {
            _store.Security.DecryptStrings(encryptedInline);
        }

        stack.Remove(offset);
    }

    private void TakeTrailer(PdfObject.Dict trailer)
    {
        if (_store.RootId == 0 && trailer.Get("Root") is PdfObject.Ref root)
        {
            _store.RootId = root.ObjectId;
            _store.RootGen = root.GenerationNumber;
        }

        if (_store.InfoId == null && _store.InlineInfo == null)
        {
            if (trailer.Get("Info") is PdfObject.Ref info)
            {
                _store.InfoId = info.ObjectId;
                _store.InfoGen = info.GenerationNumber;
            }
            else if (trailer.Get("Info") is PdfObject.Dict inline)
            {
                _store.Security?.DecryptStrings(inline);
                _store.InlineInfo = inline;
            }
        }

        if (_store.TrailerId == null && trailer.Get("ID") is PdfObject.Arr id)
        {
            _store.TrailerId = (PdfObject.Arr)id.DeepClone();
        }

        if (_store.Size == 0 && PdfValues.LongOrNull(trailer.Get("Size")) is long size)
        {
            _store.Size = size > int.MaxValue ? int.MaxValue : (int)size;
        }
    }

    private async ValueTask<bool> TryEnsureSecurityAsync(PdfObject.Dict trailer, Dictionary<int, XrefEntry> entries, string? password, bool useAsync, CancellationToken cancellationToken)
    {
        if (_store.Security != null || _store.EncryptId != 0)
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

        _store.EncryptId = reference.ObjectId;
        _store.EncryptGen = reference.GenerationNumber;
        PdfObject.Dict encrypt = await ParseEncryptDictionaryAsync(encryptOffset, reference.ObjectId, useAsync, cancellationToken).ConfigureAwait(false);
        _store.Cache[reference.ObjectId] = encrypt;
        if (!PdfSecurity.IsSupportedRevision(encrypt))
        {
            throw new PdfEncryptedException("This encryption is not supported.");
        }

        _store.Security = PdfSecurity.Create(encrypt, FileId(trailer), password, _store.EncryptId);
        return true;
    }

    private bool TryEncryptOffset(PdfObject.Ref reference, Dictionary<int, XrefEntry> entries, out long offset)
    {
        if (TryUncompressedOffset(entries, reference, out offset))
        {
            return true;
        }

        return TryUncompressedOffset(_store.Xref, reference, out offset);
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

    private async ValueTask<PdfObject.Dict> ParseEncryptDictionaryAsync(long offset, int id, bool useAsync, CancellationToken cancellationToken)
    {
        _store.Cursor.Seek(offset);
        var parser = new PdfParser(_store.Cursor, null, 0, useAsync, cancellationToken);
        if (await parser.ParseIndirectAsync(id).ConfigureAwait(false) is not PdfObject.Dict dict)
        {
            throw new PdfParseException(offset, "The encryption dictionary is not a dictionary.");
        }

        return dict;
    }

    private async ValueTask ReadXrefStreamAsync(PdfObject.Dict dict, bool overwrite, int budget, bool useAsync, CancellationToken cancellationToken)
    {
        if (dict.Body is not PdfObject.FileBody body)
        {
            throw new PdfParseException(0, "Xref stream has no data.");
        }

        if (body.Length > PdfStreamDecoder.XrefCap)
        {
            throw new PdfObjectTooLargeException("The xref stream is larger than " + PdfStreamDecoder.XrefCap.ToString(CultureInfo.InvariantCulture) + " bytes.");
        }

        byte[] raw = await _store.ReadRangeAsync(body.Offset, (int)body.Length, useAsync, cancellationToken).ConfigureAwait(false);

        // ISO 32000-1 7.5.8.2: an xref stream's /Filter and /DecodeParms are direct, so no resolver is passed.
        List<string> filters = PdfStreamDecoder.FiltersOf(dict);
        List<PdfObject.Dict?> parms = PdfStreamDecoder.ParmsOf(dict, filters.Count);
        PdfXrefReader.EnsureColumns(dict, parms);
        byte[] decoded;
        try
        {
            decoded = PdfStreamDecoder.Decode(raw, filters, parms, PdfStreamDecoder.XrefCap);
        }
        catch (PdfParseException ex)
        {
            throw new PdfParseException(body.Offset, "The xref stream could not be decoded. " + ex.Message);
        }

        _store.DecodedBytes += decoded.Length;

        PdfXrefReader.ParseStreamEntries(dict, decoded, _store.Xref, overwrite, budget);
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

    private async ValueTask<bool> IsClassicXrefAsync(long offset, bool useAsync, CancellationToken cancellationToken)
    {
        _store.Cursor.Seek(offset);
        while (true)
        {
            int value = await _store.Cursor.PeekByteAsync(useAsync, cancellationToken).ConfigureAwait(false);
            if (value < 0)
            {
                _store.Cursor.Seek(offset);
                return false;
            }

            if (value == '%')
            {
                await _store.Cursor.ReadByteAsync(useAsync, cancellationToken).ConfigureAwait(false);
                while (true)
                {
                    int comment = await _store.Cursor.ReadByteAsync(useAsync, cancellationToken).ConfigureAwait(false);
                    if (comment < 0 || comment == '\n' || comment == '\r')
                    {
                        break;
                    }
                }

                continue;
            }

            if (value is 0 or 9 or 10 or 12 or 13 or 32)
            {
                await _store.Cursor.ReadByteAsync(useAsync, cancellationToken).ConfigureAwait(false);
                continue;
            }

            break;
        }

        bool match = await _store.Cursor.ReadByteAsync(useAsync, cancellationToken).ConfigureAwait(false) == 'x' && await _store.Cursor.ReadByteAsync(useAsync, cancellationToken).ConfigureAwait(false) == 'r' && await _store.Cursor.ReadByteAsync(useAsync, cancellationToken).ConfigureAwait(false) == 'e' && await _store.Cursor.ReadByteAsync(useAsync, cancellationToken).ConfigureAwait(false) == 'f';
        _store.Cursor.Seek(offset);
        return match;
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

    internal static bool TryParseStartXref(byte[] data, int index, out long value)
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
}
