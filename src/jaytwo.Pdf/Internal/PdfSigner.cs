#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf.AcroForm;
using jaytwo.Pdf.Signatures;

namespace jaytwo.Pdf.Internal;

// Signs a document: creates or seals the signature field, reserves /ByteRange and /Contents, gets the signature
// value from the provider, and appends the signed revision.
internal sealed class PdfSigner
{
    private readonly PdfObjectStore _store;
    private readonly PdfWriteGate _gate;
    private readonly PdfPageTree _pages;
    private readonly PdfFieldTree _fields;
    private readonly PdfIncrementalWriter _writer;

    internal PdfSigner(PdfObjectStore store, PdfWriteGate gate, PdfPageTree pages, PdfFieldTree fields, PdfIncrementalWriter writer)
    {
        _store = store;
        _gate = gate;
        _pages = pages;
        _fields = fields;
        _writer = writer;
    }

    // Shared by Sign and SignAsync like SaveIncrementalAsync. The synchronous sign waits for the provider.
    internal async Task SignAsync(Stream destination, PdfSignatureOptions options, IPdfSignatureProvider provider, bool leaveOpen, bool useAsync, CancellationToken cancellationToken)
    {
        _store.CheckDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        using var release = _store.ReleaseAfter();
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

        if (options.DigestAlgorithm != PdfDigestAlgorithm.Sha256
            && (options.Kind == PdfSignatureKind.DocTimeStamp || (options.DigestAlgorithm != PdfDigestAlgorithm.Sha384 && options.DigestAlgorithm != PdfDigestAlgorithm.Sha512)))
        {
            throw new PdfSignException(options.Kind == PdfSignatureKind.DocTimeStamp
                ? "A document timestamp uses SHA-256."
                : "The digest algorithm must be SHA-256, SHA-384, or SHA-512.");
        }

        if (options.Format != PdfSignatureFormat.AdbePkcs7Detached
            && (options.Kind == PdfSignatureKind.DocTimeStamp || options.Format != PdfSignatureFormat.PadesBaselineB))
        {
            throw new PdfSignException(options.Kind == PdfSignatureKind.DocTimeStamp
                ? "A document timestamp has no signature format."
                : "The signature format must be adbe.pkcs7.detached or PAdES baseline B-B.");
        }

        _writer.ValidateDestination(destination);
        if (_store.Length > PdfIncrementalWriter.MaxClassicOffset)
        {
            throw new PdfTooLargeForClassicXrefException();
        }

        if (options.ReservedContentsBytes < 1 || options.ReservedContentsBytes > PdfSignatureOptions.MaxReservedContentsBytes)
        {
            throw new PdfSignException("Reserved contents size must be between 1 and 1048576 bytes.");
        }

        if (_fields.NeedAppearances)
        {
            throw new PdfSignException("Cannot sign while /NeedAppearances is true.");
        }

        _gate.EnsureSigningAllowed();
        _gate.RequireAnnotate();

        // A strict lower bound on the appendix, so it never refuses one that fits. The /Contents
        // gap and every dirty stream body are written in full: plain, file-backed, and RC4 bodies
        // keep their length, and AES adds an IV and padding. PdfAppendixBuffer is the exact check.
        long estimate = 2L + (options.ReservedContentsBytes * 2L);
        foreach (int id in _store.Dirty)
        {
            if (_store.Cache.TryGetValue(id, out PdfObject? obj) && obj is PdfObject.Dict dict && dict.Body != null)
            {
                estimate += dict.Body.Length;
            }
        }

        if (estimate > PdfAppendixBuffer.MaxLength)
        {
            throw new PdfSignException(PdfAppendixBuffer.TooLargeMessage);
        }

        int savedSize = _store.Size;
        int? savedInfo = _store.InfoId;
        int savedInfoGen = _store.InfoGen;
        PdfObject.Arr? savedId = _store.TrailerId;
        SignatureState? savedSignatureState = _gate.CachedSignatureState;
        var savedDirty = new HashSet<int>(_store.Dirty);
        _store.BeginBackup();
        try
        {
            if (_store.TrailerId == null)
            {
                _store.TrailerId = NewId();
            }

            DateTimeOffset? signingTime = options.Kind == PdfSignatureKind.DocTimeStamp
                ? null
                : options.SigningTime ?? DateTimeOffset.UtcNow;
            BuildSignature(options, signingTime);
            _gate.CachedSignatureState = null;

            // The estimate above runs before BuildSignature dirties a reused field. The bounded
            // buffer is the exact guard: it throws before the destination is touched.
            using var buffer = new PdfAppendixBuffer();
            if (_writer.NeedsLeadingLineFeed())
            {
                buffer.WriteByte((byte)'\n');
            }

            AppendixLayout layout = _writer.WriteAppendix(buffer, _store.Length);
            if (layout.Contents == null || layout.ByteRange == null)
            {
                throw new PdfSignException("The signature dictionary did not reserve /ByteRange and /Contents.");
            }

            long gap = 2L + (options.ReservedContentsBytes * 2L);
            long less = _store.Length + layout.Contents.LessThanPosition;
            long total = _store.Length + buffer.Length;
            var range = new PdfByteRange(0, less, less + gap, total - less - gap);
            PatchAscii(buffer, layout.ByteRange.DigitPosition, Ten(range.Offset0) + " " + Ten(range.Length0) + " " + Ten(range.Offset1) + " " + Ten(range.Length1));
            var context = new PdfSignatureContext(range, options.ReservedContentsBytes, signingTime, options.DigestAlgorithm, options.Format, () => _writer.OpenSigned(buffer, layout.Contents.LessThanPosition, gap));
            ValueTask<byte[]> pending = provider.CreateContentsAsync(context, cancellationToken);
            byte[] contents = useAsync ? await pending.ConfigureAwait(false) : pending.AsTask().GetAwaiter().GetResult();
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
            cancellationToken.ThrowIfCancellationRequested();
            bool same = _writer.IsSourceStream(destination);
            using var appending = _writer.BeginAppend(same);
            try
            {
                if (!same)
                {
                    await _writer.CopyPrefixAsync(destination, useAsync, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    destination.Seek(_store.Length, SeekOrigin.Begin);
                }

                if (useAsync)
                {
                    await destination.WriteAsync(buffer.GetBuffer(), 0, (int)buffer.Length, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    buffer.WriteTo(destination);
                }
            }
            catch
            {
                PdfIncrementalWriter.Truncate(destination, same ? _store.Length : 0);
                throw;
            }

            _writer.FinishWrite(destination, leaveOpen, same, layout);
            _store.EndBackup();
        }
        catch
        {
            _store.Restore(savedSize, savedInfo, savedInfoGen, savedId, savedDirty);
            _gate.CachedSignatureState = savedSignatureState;
            throw;
        }
    }

    private void BuildSignature(PdfSignatureOptions options, DateTimeOffset? signingTime)
    {
        // Signature fields and dictionaries and /SigFlags are PDF 1.3. ISO 32000-1 does not define /ETSI.CAdES.detached,
        // /ETSI.RFC3161, or /DocTimeStamp, so they raise nothing (spec 7.5).
        _store.RaiseVersion(13);
        List<FieldSlot> existing = FindNamedFields(options.FieldName);
        if (existing.Count > 0)
        {
            SealExistingField(existing, options, signingTime);
            return;
        }

        List<PageInfo> pages = _pages.GetPages();
        if (options.PageNumber < 1 || options.PageNumber > pages.Count)
        {
            throw new PdfSignException("The signature page is outside the document.");
        }

        // Spec 7.3: no right grants a signature that creates a field, so /UR3 is removed.
        _gate.ApplyUsageRights(null, null);
        PageInfo page = pages[options.PageNumber - 1];
        string fieldName = options.FieldName ?? string.Empty;
        if (fieldName.Length == 0)
        {
            var names = new HashSet<string>(_fields.CollectFields().Select(slot => slot.FullName));
            int number = 1;
            while (names.Contains("Signature" + number.ToString(CultureInfo.InvariantCulture)))
            {
                number++;
            }

            fieldName = "Signature" + number.ToString(CultureInfo.InvariantCulture);
        }

        int signatureId = _store.Allocate(CreateSignatureDictionary(options, signingTime));
        int pageGen = _store.Load(page.ObjectId).Generation;
        var widget = new PdfObject.Dict();
        widget.Set("Type", new PdfObject.Name("Annot"));
        widget.Set("Subtype", new PdfObject.Name("Widget"));
        widget.Set("FT", new PdfObject.Name("Sig"));
        widget.Set("T", PdfTextCodec.Encode(fieldName));
        widget.Set("V", new PdfObject.Ref(signatureId, 0));
        widget.Set("Rect", PdfValues.RectArray(options.Rect));
        widget.Set("F", PdfObject.Num.FromLong(4));
        widget.Set("P", new PdfObject.Ref(page.ObjectId, pageGen));
        int widgetId = _store.Allocate(widget);
        PdfObject.Dict pageDict = (PdfObject.Dict)_store.EditAny(page.ObjectId);
        _store.EditArray(pageDict, "Annots").Items.Add(new PdfObject.Ref(widgetId, 0));
        PdfObject.Dict acro = _fields.EnsureAcroForm();
        _store.EditArray(acro, "Fields").Items.Add(new PdfObject.Ref(widgetId, 0));
        SetSignatureFlags(acro);
    }

    private List<FieldSlot> FindNamedFields(string? fieldName)
    {
        if (string.IsNullOrEmpty(fieldName))
        {
            return new List<FieldSlot>();
        }

        return _fields.CollectFields().Where(slot => slot.FullName == fieldName).ToList();
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
            if (_gate.HasSignatureDictionary(slot))
            {
                throw new PdfSignException("The signature field already has a signature dictionary.");
            }
        }

        // Spec 7.3: /Signature Modify grants signing an existing empty field (ISO 32000-1 Table 255). It does not
        // grant a document timestamp.
        if (options.Kind == PdfSignatureKind.DocTimeStamp)
        {
            _gate.ApplyUsageRights(null, null);
        }
        else
        {
            _gate.ApplyUsageRights("Signature", "Modify");
        }

        int signatureId = _store.Allocate(CreateSignatureDictionary(options, signingTime));
        var owners = new HashSet<int>();
        foreach (FieldSlot slot in matches)
        {
            if (!owners.Add(slot.ValueObjectId))
            {
                continue;
            }

            PdfObject.Dict owner = (PdfObject.Dict)_store.EditAny(slot.ValueObjectId);
            owner.Set("V", new PdfObject.Ref(signatureId, 0));
        }

        SetSignatureFlags(_fields.EnsureAcroForm());
    }

    private static void SetSignatureFlags(PdfObject.Dict acro)
    {
        acro.Set("SigFlags", PdfObject.Num.FromLong(PdfValues.IntOf(acro.Get("SigFlags")) | 3));
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
            signature.Set("SubFilter", new PdfObject.Name(options.Format == PdfSignatureFormat.PadesBaselineB ? "ETSI.CAdES.detached" : "adbe.pkcs7.detached"));
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

    private static void SetOptional(PdfObject.Dict dict, string key, string? value)
    {
        if (value != null)
        {
            dict.Set(key, PdfTextCodec.Encode(value));
        }
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

    private static void PatchAscii(Stream stream, long position, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        stream.Position = position;
        stream.Write(bytes, 0, bytes.Length);
    }

    private static string Ten(long value)
    {
        if (value < 0 || value > PdfIncrementalWriter.MaxClassicOffset)
        {
            throw new PdfTooLargeForClassicXrefException();
        }

        return value.ToString("D10", CultureInfo.InvariantCulture);
    }
}
