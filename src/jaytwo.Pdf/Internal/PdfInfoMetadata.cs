#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using jaytwo.Pdf.Metadata;

namespace jaytwo.Pdf.Internal;

// The document information dictionary and the XMP packet, kept consistent on every write.
internal sealed class PdfInfoMetadata
{
    private readonly PdfObjectStore _store;
    private readonly PdfWriteGate _gate;
    private bool _modDateExplicit;
    private bool _touchingModDate;

    internal PdfInfoMetadata(PdfObjectStore store, PdfWriteGate gate)
    {
        _store = store;
        _gate = gate;
    }

    internal string? GetInfoText(string key)
    {
        _store.CheckDisposed();
        using var release = _store.ReleaseAfter();
        PdfObject.Dict? info = ReadInfo();
        return info == null ? null : _store.ValueText(info.Get(key));
    }

    internal IReadOnlyList<string> InfoKeys()
    {
        _store.CheckDisposed();
        using var release = _store.ReleaseAfter();
        PdfObject.Dict? info = ReadInfo();
        if (info == null)
        {
            return Array.Empty<string>();
        }

        var keys = new List<string>(info.Items.Count);
        foreach ((string key, _) in info.Items)
        {
            keys.Add(key);
        }

        return keys;
    }

    internal void SetInfo(string key, string? value)
    {
        if (key == null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (key.Length == 0)
        {
            throw new ArgumentException("The document information key is empty.", nameof(key));
        }

        using var release = _store.ReleaseAfter();
        if (key == "CreationDate" || key == "ModDate")
        {
            SetInfoDate(key, value == null ? null : PdfTextCodec.ParseDate(value), key == "ModDate");
            return;
        }

        if (key == "Trapped")
        {
            SetTrapped(value);
            return;
        }

        SetInfoText(key, value);
    }

    internal void SetInfoText(string key, string? value)
    {
        _store.CheckDisposed();
        using var release = _store.ReleaseAfter();
        _gate.EnsureDocumentChange();
        _gate.RequireModify();
        _gate.ApplyUsageRights(null, null);
        PdfObject.Dict info = EditInfo();
        if (value == null)
        {
            info.Remove(key);
        }
        else
        {
            info.Set(key, PdfTextCodec.Encode(value));
            _store.RaiseVersion(InfoKeyVersion(key));
        }

        SyncXmp(key, value);
        TouchModDate();
    }

    private void SetTrapped(string? value)
    {
        _store.CheckDisposed();
        if (value != null && value != "True" && value != "False" && value != "Unknown")
        {
            throw new ArgumentException("Trapped must be True, False, or Unknown.", nameof(value));
        }

        _gate.EnsureDocumentChange();
        _gate.RequireModify();
        _gate.ApplyUsageRights(null, null);
        PdfObject.Dict info = EditInfo();
        if (value == null)
        {
            info.Remove("Trapped");
        }
        else
        {
            info.Set("Trapped", new PdfObject.Name(value));
            _store.RaiseVersion(InfoKeyVersion("Trapped"));
        }

        TouchModDate();
    }

    internal DateTimeOffset? GetInfoDate(string key)
    {
        _store.CheckDisposed();
        using var release = _store.ReleaseAfter();
        string? text = GetInfoText(key);
        return text == null ? null : PdfTextCodec.ParseDate(text);
    }

    internal void SetInfoDate(string key, DateTimeOffset? value, bool explicitModification)
    {
        _store.CheckDisposed();
        using var release = _store.ReleaseAfter();
        _gate.EnsureDocumentChange();
        _gate.RequireModify();
        _gate.ApplyUsageRights(null, null);
        if (explicitModification)
        {
            _modDateExplicit = true;
        }

        PdfObject.Dict info = EditInfo();
        string? xmp = null;
        if (value == null)
        {
            info.Remove(key);
        }
        else
        {
            info.Set(key, PdfTextCodec.Encode(PdfTextCodec.FormatDate(value.Value)));
            _store.RaiseVersion(InfoKeyVersion(key));
            xmp = PdfTextCodec.FormatXmpDate(value.Value);
        }

        SyncXmp(key, xmp);
        if (!explicitModification)
        {
            TouchModDate();
        }
    }

    internal Stream OpenXmp()
    {
        _store.CheckDisposed();
        using var release = _store.ReleaseAfter();
        byte[]? bytes = TryReadXmpBytes() ?? throw new PdfXmpException("The document has no XMP metadata.");
        if (!XmpEditor.TryParse(bytes, out _, out Exception? error))
        {
            throw new PdfXmpException("XMP is not well-formed XML.", error);
        }

        return new MemoryStream(bytes, false);
    }

    internal void SetXmp(ReadOnlySpan<byte> utf8Packet)
    {
        _store.CheckDisposed();
        using var release = _store.ReleaseAfter();
        _gate.EnsureDocumentChange();
        _gate.RequireModify();
        _gate.ApplyUsageRights(null, null);
        var copy = new byte[utf8Packet.Length];
        utf8Packet.CopyTo(copy);
        WriteXmp(copy);
    }

    // ISO 32000-1 Table 317. Author, Creator, Producer, CreationDate, and custom keys are PDF 1.0.
    private static int InfoKeyVersion(string key)
    {
        return key switch
        {
            "Title" or "Subject" or "Keywords" or "ModDate" => 11,
            "Trapped" => 13,
            _ => 10,
        };
    }

    private PdfObject.Dict? ReadInfo()
    {
        if (_store.InlineInfo != null && _store.InfoId == null)
        {
            return _store.InlineInfo;
        }

        if (_store.InfoId == null)
        {
            return null;
        }

        return _store.Resolve(new PdfObject.Ref(_store.InfoId.Value, _store.InfoGen)) as PdfObject.Dict;
    }

    private PdfObject.Dict EditInfo()
    {
        if (_store.InfoId != null && (!_store.IsLive(new PdfObject.Ref(_store.InfoId.Value, _store.InfoGen)) || ReadInfo() == null))
        {
            _store.InfoId = null;
        }

        if (_store.InfoId == null)
        {
            var created = _store.InlineInfo == null ? new PdfObject.Dict() : (PdfObject.Dict)_store.InlineInfo.DeepClone();
            _store.InlineInfo = null;
            _store.InfoId = _store.Allocate(created);
            _store.InfoGen = 0;
            return created;
        }

        return (PdfObject.Dict)_store.EditAny(_store.InfoId.Value);
    }

    private void TouchModDate()
    {
        if (_modDateExplicit || _touchingModDate)
        {
            return;
        }

        _touchingModDate = true;
        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            PdfObject.Dict info = EditInfo();
            info.Set("ModDate", PdfTextCodec.Encode(PdfTextCodec.FormatDate(now)));
            _store.RaiseVersion(InfoKeyVersion("ModDate"));
            SyncXmp("ModDate", PdfTextCodec.FormatXmpDate(now));
        }
        finally
        {
            _touchingModDate = false;
        }
    }

    private void SyncXmp(string key, string? value)
    {
        if (key != "Title" && key != "Author" && key != "Subject" && key != "Keywords" && key != "Creator" && key != "Producer" && key != "CreationDate" && key != "ModDate")
        {
            return;
        }

        byte[]? existing = TryReadXmpBytes();
        XDocument document;
        if (existing == null)
        {
            document = XmpEditor.CreateMinimal();
        }
        else if (!XmpEditor.TryParse(existing, out XDocument? parsed, out _))
        {
            return;
        }
        else
        {
            document = parsed!;
        }

        // A packet that parses but is not XMP, such as one without rdf:RDF, is malformed XMP too: leave its bytes unchanged.
        try
        {
            XmpEditor.ApplyInfo(document, key, value);
        }
        catch (PdfXmpException)
        {
            return;
        }

        WriteXmp(XmpEditor.ToBytes(document));
    }

    private byte[]? TryReadXmpBytes()
    {
        if (_store.ResolveMaybe(_store.CatalogDict().Get("Metadata")) is not PdfObject.Dict dict)
        {
            return null;
        }

        return _store.ReadDecoded(dict, PdfStreamDecoder.StructuralCap);
    }

    private void WriteXmp(byte[] bytes)
    {
        PdfObject.Dict? metadata = _store.ResolveMaybe(_store.CatalogDict().Get("Metadata")) as PdfObject.Dict;
        PdfObject.Dict target;
        if (metadata == null || metadata.ObjectNumber == 0)
        {
            target = new PdfObject.Dict();
            target.Set("Type", new PdfObject.Name("Metadata"));
            target.Set("Subtype", new PdfObject.Name("XML"));
            int id = _store.Allocate(target);
            _store.EditCatalog().Set("Metadata", new PdfObject.Ref(id, 0));
        }
        else
        {
            target = (PdfObject.Dict)_store.EditAny(metadata.ObjectNumber);
            target.Remove("Filter");
            target.Remove("DecodeParms");
        }

        target.Set("Type", new PdfObject.Name("Metadata"));
        target.Set("Subtype", new PdfObject.Name("XML"));
        target.Set("Length", PdfObject.Num.FromLong(bytes.Length));
        target.Body = new PdfObject.MemoryBody(bytes);
        _store.RaiseVersion(14);
    }
}
