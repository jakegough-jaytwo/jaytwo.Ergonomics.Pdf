using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf.AcroForm;
using jaytwo.Pdf.Attachments;
using jaytwo.Pdf.Signatures;

namespace jaytwo.Pdf.Internal;

// Opens a document and routes each inspection, edit, save, and signature to the type that owns it.
internal sealed class PdfEngine : IDisposable
{
    private readonly PdfObjectStore _store;
    private readonly PdfWriteGate _gate;
    private readonly PdfPageTree _pages;
    private readonly PdfInfoMetadata _info;
    private readonly PdfFieldTree _fields;
    private readonly PdfFormFiller _filler;
    private readonly PdfFlattener _flattener;
    private readonly PdfEmbeddedFiles _files;
    private readonly PdfSignatureInspector _inspector;
    private readonly PdfIncrementalWriter _writer;
    private readonly PdfSigner _signer;

    private PdfEngine(IPdfSource source, bool ownsSource, int cursorWindows)
    {
        _store = new PdfObjectStore(source, ownsSource, cursorWindows);
        _gate = new PdfWriteGate(_store);
        _pages = new PdfPageTree(_store, _gate);
        _info = new PdfInfoMetadata(_store, _gate);
        _fields = new PdfFieldTree(_store, _pages);
        var appearances = new PdfFormAppearances(_store, _fields);
        _filler = new PdfFormFiller(_store, _gate, _fields, appearances);
        _flattener = new PdfFlattener(_store, _gate, _pages, _fields);
        _files = new PdfEmbeddedFiles(this, _store, _gate);
        _inspector = new PdfSignatureInspector(_store, _gate, _fields);
        _writer = new PdfIncrementalWriter(_store, _gate);
        _signer = new PdfSigner(_store, _gate, _pages, _fields, _writer);
    }

    internal int PageCount
    {
        get
        {
            using var release = _store.ReleaseAfter();
            return _pages.GetPages().Count;
        }
    }

    internal int RetainedObjectStreamCount => _store.RetainedObjectStreamCount;

    // The bytes the engine has produced by decoding cross-reference, object, and other streams it reads whole.
    internal long DecodedByteCount => _store.DecodedBytes;

    internal long CursorPeakRetainedBytes => _store.Cursor.PeakRetainedBytes;

    internal PdfPermissions Permissions
    {
        get
        {
            _store.CheckDisposed();
            return _store.Security == null ? PdfPermissions.AllowAll() : _store.Security.Permissions;
        }
    }

    internal bool HasFields
    {
        get
        {
            using var release = _store.ReleaseAfter();
            return _fields.CollectFields().Count > 0;
        }
    }

    internal bool NeedAppearances => _fields.NeedAppearances;

    internal bool IsHybridXfa
    {
        get
        {
            using var release = _store.ReleaseAfter();
            return _fields.ClassifyXfa(_fields.CollectFields()) == XfaKind.Hybrid;
        }
    }

    internal bool IsXfaOnly
    {
        get
        {
            using var release = _store.ReleaseAfter();
            return _fields.ClassifyXfa(_fields.CollectFields()) == XfaKind.XfaOnly;
        }
    }

    internal bool HasXmp
    {
        get
        {
            using var release = _store.ReleaseAfter();
            return _store.CatalogDict().Get("Metadata") != null;
        }
    }

    internal bool HasUsageRights
    {
        get
        {
            using var release = _store.ReleaseAfter();
            return _store.ResolveMaybe(_store.CatalogDict().Get("Perms")) is PdfObject.Dict perms && perms.Get("UR3") != null;
        }
    }

    internal bool AllowBreakingSignatures
    {
        get => _gate.AllowBreakingSignatures;
        set => _gate.AllowBreakingSignatures = value;
    }

    public void Dispose()
    {
        _store.Dispose();
    }

    internal static PdfEngine Open(IPdfSource source, bool ownsSource, string? password, int cursorWindows)
    {
        return PdfSync.Result(OpenAsync(source, ownsSource, password, cursorWindows, false, CancellationToken.None));
    }

    internal static ValueTask<PdfEngine> OpenAsync(IPdfSource source, bool ownsSource, string? password, int cursorWindows, CancellationToken cancellationToken)
    {
        return OpenAsync(source, ownsSource, password, cursorWindows, true, cancellationToken);
    }

    internal IReadOnlyList<PdfPage> ListPages()
    {
        return _pages.ListPages();
    }

    internal int SetPageRotation(int objectId, int rotation)
    {
        return _pages.SetPageRotation(objectId, rotation);
    }

    internal string? GetInfoText(string key)
    {
        return _info.GetInfoText(key);
    }

    internal IReadOnlyList<string> InfoKeys()
    {
        return _info.InfoKeys();
    }

    internal void SetInfo(string key, string? value)
    {
        _info.SetInfo(key, value);
    }

    internal void SetInfoText(string key, string? value)
    {
        _info.SetInfoText(key, value);
    }

    internal DateTimeOffset? GetInfoDate(string key)
    {
        return _info.GetInfoDate(key);
    }

    internal void SetInfoDate(string key, DateTimeOffset? value, bool explicitModification)
    {
        _info.SetInfoDate(key, value, explicitModification);
    }

    internal Stream OpenXmp()
    {
        return _info.OpenXmp();
    }

    internal void SetXmp(ReadOnlySpan<byte> utf8Packet)
    {
        _info.SetXmp(utf8Packet);
    }

    internal IReadOnlyList<PdfField> ListFields()
    {
        return _fields.ListFields();
    }

    internal IReadOnlyList<PdfField> GetFields(string fullName)
    {
        return _fields.GetFields(fullName);
    }

    internal IReadOnlyDictionary<string, PdfAppearanceDisposition> Fill(IEnumerable<KeyValuePair<string, PdfFieldValue>> values)
    {
        return _filler.Fill(values);
    }

    internal IReadOnlyDictionary<string, PdfFieldValue> Export()
    {
        return _fields.Export();
    }

    internal void Flatten(IEnumerable<string>? fullNames)
    {
        _flattener.Flatten(fullNames);
    }

    internal IReadOnlyList<PdfAttachment> ListAttachments()
    {
        return _files.ListAttachments();
    }

    internal PdfAttachment AddAttachment(string name, Stream content, bool leaveOpen, PdfAttachmentOptions? options)
    {
        return _files.AddAttachment(name, content, leaveOpen, options);
    }

    internal void RemoveAttachment(string name)
    {
        _files.RemoveAttachment(name);
    }

    internal Stream OpenAttachment(string name)
    {
        return _files.OpenAttachment(name);
    }

    internal IReadOnlyList<PdfSignatureInfo> ListSignatures()
    {
        return _inspector.ListSignatures();
    }

    internal IReadOnlyList<PdfRevision> ListRevisions()
    {
        return _inspector.ListRevisions();
    }

    internal void SaveIncremental()
    {
        using var release = _store.ReleaseAfter();
        SaveIncremental(_writer.SourceStream(), true);
    }

    internal void SaveIncremental(Stream destination, bool leaveOpen)
    {
        _writer.SaveIncrementalAsync(destination, leaveOpen, false, CancellationToken.None).GetAwaiter().GetResult();
    }

    internal Task SaveIncrementalAsync(CancellationToken cancellationToken)
    {
        return _writer.SaveIncrementalAsync(_writer.SourceStream(), true, true, cancellationToken);
    }

    internal Task SaveIncrementalAsync(Stream destination, bool leaveOpen, CancellationToken cancellationToken)
    {
        return _writer.SaveIncrementalAsync(destination, leaveOpen, true, cancellationToken);
    }

    internal void Sign(PdfSignatureOptions options, IPdfSignatureProvider provider)
    {
        using var release = _store.ReleaseAfter();
        Sign(_writer.SourceStream(), options, provider, true);
    }

    internal void Sign(Stream destination, PdfSignatureOptions options, IPdfSignatureProvider provider, bool leaveOpen)
    {
        _signer.SignAsync(destination, options, provider, leaveOpen, false, CancellationToken.None).GetAwaiter().GetResult();
    }

    internal Task SignAsync(PdfSignatureOptions options, IPdfSignatureProvider provider, CancellationToken cancellationToken)
    {
        return _signer.SignAsync(_writer.SourceStream(), options, provider, true, true, cancellationToken);
    }

    internal Task SignAsync(Stream destination, PdfSignatureOptions options, IPdfSignatureProvider provider, bool leaveOpen, CancellationToken cancellationToken)
    {
        return _signer.SignAsync(destination, options, provider, leaveOpen, true, cancellationToken);
    }

    // Open is not an operation lease, so it releases the cursor windows it filled itself. A failed or canceled open
    // disposes the engine it built, so no partially parsed state is published; an owned source is disposed with it.
    private static async ValueTask<PdfEngine> OpenAsync(IPdfSource source, bool ownsSource, string? password, int cursorWindows, bool useAsync, CancellationToken cancellationToken)
    {
        var engine = new PdfEngine(source, ownsSource, cursorWindows);
        try
        {
            await new PdfDocumentLoader(engine._store).InitializeAsync(password, useAsync, cancellationToken).ConfigureAwait(false);
            return engine;
        }
        catch
        {
            engine.Dispose();
            throw;
        }
        finally
        {
            engine._store.Cursor.Release();
        }
    }
}
