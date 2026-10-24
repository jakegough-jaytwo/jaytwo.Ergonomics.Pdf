#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using jaytwo.Pdf.Attachments;

namespace jaytwo.Pdf.Internal;

// Document-level attachments: the /EmbeddedFiles name tree and the catalog /AF array.
internal sealed class PdfEmbeddedFiles
{
    private readonly PdfEngine _engine;
    private readonly PdfObjectStore _store;
    private readonly PdfWriteGate _gate;

    internal PdfEmbeddedFiles(PdfEngine engine, PdfObjectStore store, PdfWriteGate gate)
    {
        _engine = engine;
        _store = store;
        _gate = gate;
    }

    internal IReadOnlyList<PdfAttachment> ListAttachments()
    {
        _store.CheckDisposed();
        using var release = _store.ReleaseAfter();
        var list = new List<PdfAttachment>();
        foreach (AttachmentSlot slot in ReadAttachments())
        {
            list.Add(new PdfAttachment(_engine, slot.Name, slot.Description, slot.MimeType, slot.Size, slot.Relationship, slot.CreationDate, slot.ModificationDate));
        }

        return list;
    }

    internal PdfAttachment AddAttachment(string name, Stream content, bool leaveOpen, PdfAttachmentOptions? options)
    {
        _store.CheckDisposed();
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (!content.CanSeek)
        {
            throw new ArgumentException("Attachment content must be a seekable stream.", nameof(content));
        }

        PdfAttachmentRelationship relationship = options?.Relationship ?? PdfAttachmentRelationship.Unspecified;
        if (RelationshipOf(relationship.ToString()) != relationship)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The attachment relationship is not defined.");
        }

        using var release = _store.ReleaseAfter();
        _gate.EnsureDocumentChange();
        _gate.RequireModify();
        List<AttachmentSlot> slots = ReadAttachments();
        AttachmentSlot? previous = slots.FirstOrDefault(slot => slot.Name == name && slot.KeyInTree);
        long start = content.Position;
        long length = content.Length - start;

        // Spec W10 and W11: the extra streaming pass for /CheckSum and the compressed /Length. It runs after every
        // check that can refuse the write and before the document is dirtied.
        bool compress = options?.Compress == true;
        PdfObject.ExternalBody body = PdfObject.ExternalBody.Read(content, start, length, compress);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string creationDate = PdfTextCodec.FormatDate(options?.CreationDate ?? now);
        string modificationDate = PdfTextCodec.FormatDate(options?.ModificationDate ?? now);
        bool pageMode = options?.OpenAttachmentsPanel == true && PdfValues.NameOf(_store.ResolveMaybe(_store.CatalogDict().Get("PageMode"))) != "UseAttachments";
        int replaced = slots.RemoveAll(slot => slot.Name == name);

        // No UR3 right covers the catalog /PageMode (ISO 32000-1 Table 255), so a page-mode change is a write the
        // transform cannot grant (spec 7.3).
        if (pageMode)
        {
            _gate.ApplyUsageRights(null, null);
        }
        else
        {
            _gate.ApplyUsageRights("EF", replaced == 0 ? "Create" : "Modify");
        }

        var embedded = new PdfObject.Dict();
        embedded.Set("Type", new PdfObject.Name("EmbeddedFile"));
        if (options?.MimeType is string mimeType && mimeType.Length > 0)
        {
            embedded.Set("Subtype", new PdfObject.Name(mimeType));
        }

        if (compress)
        {
            embedded.Set("Filter", new PdfObject.Name("FlateDecode"));
        }

        // ISO 32000-1 Table 46: /CheckSum is the MD5 of the uncompressed bytes, a byte string.
        var parameters = new PdfObject.Dict();
        parameters.Set("Size", PdfObject.Num.FromLong(length));
        parameters.Set("CreationDate", PdfTextCodec.Encode(creationDate));
        parameters.Set("ModDate", PdfTextCodec.Encode(modificationDate));
        parameters.Set("CheckSum", new PdfObject.Str(body.Checksum, true));
        embedded.Set("Params", parameters);
        embedded.Set("Length", PdfObject.Num.FromLong(body.Length));
        embedded.Body = body;
        int streamId = _store.Allocate(embedded);
        var spec = new PdfObject.Dict();
        spec.Set("Type", new PdfObject.Name("Filespec"));
        spec.Set("F", PdfTextCodec.Encode(AsciiFallback(name)));
        spec.Set("UF", PdfTextCodec.Encode(name));
        var ef = new PdfObject.Dict();
        ef.Set("F", new PdfObject.Ref(streamId, 0));
        ef.Set("UF", new PdfObject.Ref(streamId, 0));
        spec.Set("EF", ef);
        spec.Set("AFRelationship", new PdfObject.Name(relationship.ToString()));
        if (options?.Description is string description && description.Length > 0)
        {
            spec.Set("Desc", PdfTextCodec.Encode(description));
        }

        int specId = _store.Allocate(spec);
        if (!leaveOpen)
        {
            _store.OwnedStreams.Add(content);
        }

        // A replaced attachment keeps the key the tree held; WriteAttachments encodes a new one.
        slots.Add(new AttachmentSlot(name, previous?.SortKey ?? Array.Empty<byte>(), specId, options?.Description, options?.MimeType, length, streamId)
        {
            KeyInTree = previous != null,
            Relationship = relationship,
            CreationDate = creationDate,
            ModificationDate = modificationDate,
        });
        WriteAttachments(slots);
        if (pageMode)
        {
            _store.EditCatalog().Set("PageMode", new PdfObject.Name("UseAttachments"));
        }

        // /UF in the file specification and in /EF is PDF 1.7 (ISO 32000-1 Table 44), which covers /PageMode
        // /UseAttachments (PDF 1.6, Table 28); /AF and /AFRelationship raise nothing (spec 7.5).
        _store.RaiseVersion(17);
        return new PdfAttachment(_engine, name, options?.Description, options?.MimeType, length, relationship, creationDate, modificationDate);
    }

    internal void RemoveAttachment(string name)
    {
        _store.CheckDisposed();
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        using var release = _store.ReleaseAfter();
        _gate.EnsureDocumentChange();
        _gate.RequireModify();
        List<AttachmentSlot> slots = ReadAttachments();
        if (slots.RemoveAll(slot => slot.Name == name) == 0)
        {
            throw new PdfAttachmentNotFoundException(name);
        }

        _gate.ApplyUsageRights("EF", "Delete");
        WriteAttachments(slots);
    }

    internal Stream OpenAttachment(string name)
    {
        _store.CheckDisposed();
        using var release = _store.ReleaseAfter();
        AttachmentSlot slot = ReadAttachments().FirstOrDefault(item => item.Name == name) ?? throw new PdfAttachmentNotFoundException(name);
        if (slot.StreamId == 0 || _store.Load(slot.StreamId) is not PdfObject.Dict dict)
        {
            throw new PdfParseException(0, "Embedded file is not a stream.");
        }

        return _store.OpenBody(dict);
    }

    private List<AttachmentSlot> ReadAttachments()
    {
        var list = new List<AttachmentSlot>();
        var seen = new HashSet<int>();
        PdfObject.Dict catalog = _store.CatalogDict();
        if (_store.ResolveMaybe(catalog.Get("Names")) is PdfObject.Dict names)
        {
            PdfObject? embeddedFiles = names.Get("EmbeddedFiles");
            if (_store.ResolveMaybe(embeddedFiles) is PdfObject.Dict tree)
            {
                int treeId = embeddedFiles is PdfObject.Ref reference ? reference.ObjectId : tree.ObjectNumber;
                WalkNames(tree, treeId, list, seen, new HashSet<int>(), 0);
            }
        }

        if (_store.ResolveMaybe(catalog.Get("AF")) is PdfObject.Arr af)
        {
            foreach (PdfObject item in af.Items)
            {
                if (item is PdfObject.Ref reference && seen.Add(reference.ObjectId) && _store.Resolve(reference) is PdfObject.Dict spec)
                {
                    list.Add(ToAttachment(spec, reference.ObjectId));
                }
            }
        }

        return list;
    }

    private void WalkNames(PdfObject.Dict node, int nodeId, List<AttachmentSlot> list, HashSet<int> seen, HashSet<int> seenNodes, int depth)
    {
        if (nodeId != 0 && !seenNodes.Add(nodeId))
        {
            throw new PdfParseException(0, "The embedded-file name tree contains a cycle.");
        }

        if (depth > PdfObjectStore.MaxTraversalDepth)
        {
            throw new PdfParseException(0, "The embedded-file name tree is too deep.");
        }

        if (_store.ResolveMaybe(node.Get("Names")) is PdfObject.Arr names)
        {
            for (int i = 0; i + 1 < names.Items.Count; i += 2)
            {
                if (names.Items[i] is not PdfObject.Str key || _store.ResolveMaybe(names.Items[i + 1]) is not PdfObject.Dict spec)
                {
                    continue;
                }

                int id = names.Items[i + 1] is PdfObject.Ref reference ? reference.ObjectId : spec.ObjectNumber;
                if (id != 0 && !seen.Add(id))
                {
                    continue;
                }

                // A name-tree key is a string, not necessarily a text string (ISO 32000-1 7.9.6). A key that is not
                // text keeps the file specification's name (/UF, then /F); its bytes stay the key on a later write.
                AttachmentSlot slot = ToAttachment(spec, id);
                slot.SortKey = key.Raw;
                slot.KeyInTree = true;
                if (PdfTextCodec.TryDecode(key.Raw, out string keyText, out _))
                {
                    slot.Name = keyText;
                }

                list.Add(slot);
            }
        }

        if (_store.ResolveMaybe(node.Get("Kids")) is PdfObject.Arr kids)
        {
            foreach (PdfObject kid in kids.Items)
            {
                if (_store.ResolveMaybe(kid) is PdfObject.Dict child)
                {
                    int childId = kid is PdfObject.Ref reference ? reference.ObjectId : child.ObjectNumber;
                    WalkNames(child, childId, list, seen, seenNodes, depth + 1);
                }
            }
        }
    }

    private AttachmentSlot ToAttachment(PdfObject.Dict spec, int specId)
    {
        string name = _store.ValueText(spec.Get("UF")) ?? _store.ValueText(spec.Get("F")) ?? string.Empty;
        byte[] sort = spec.Get("UF") is PdfObject.Str uf ? uf.Raw : spec.Get("F") is PdfObject.Str f ? f.Raw : PdfTextCodec.Encode(name).Raw;
        string? description = _store.ValueText(spec.Get("Desc"));
        int streamId = 0;
        string? mime = null;
        long? size = null;
        string? creationDate = null;
        string? modificationDate = null;
        if (_store.ResolveMaybe(spec.Get("EF")) is PdfObject.Dict ef)
        {
            PdfObject? file = ef.Get("UF") ?? ef.Get("F");
            if (_store.ResolveMaybe(file) is PdfObject.Dict stream)
            {
                if (file is PdfObject.Ref reference)
                {
                    streamId = reference.ObjectId;
                }

                mime = PdfValues.NameOf(stream.Get("Subtype"));
                if (_store.ResolveMaybe(stream.Get("Params")) is PdfObject.Dict parameters)
                {
                    size = PdfValues.LongOrNull(parameters.Get("Size"));
                    creationDate = _store.ResolveMaybe(parameters.Get("CreationDate")) is PdfObject.Str created ? PdfTextCodec.Decode(created.Raw) : null;
                    modificationDate = _store.ResolveMaybe(parameters.Get("ModDate")) is PdfObject.Str modified ? PdfTextCodec.Decode(modified.Raw) : null;
                }
            }
        }

        return new AttachmentSlot(name, sort, specId, description, mime, size, streamId)
        {
            Relationship = RelationshipOf(PdfValues.NameOf(_store.ResolveMaybe(spec.Get("AFRelationship")))),
            CreationDate = creationDate,
            ModificationDate = modificationDate,
        };
    }

    // ISO 32000-2 Table 43. An absent value, and a name that is not one of these, such as a second-class name, read
    // as Unspecified, the value the standard gives a relationship that is not known.
    private static PdfAttachmentRelationship RelationshipOf(string? name)
    {
        return name switch
        {
            "Source" => PdfAttachmentRelationship.Source,
            "Data" => PdfAttachmentRelationship.Data,
            "Alternative" => PdfAttachmentRelationship.Alternative,
            "Supplement" => PdfAttachmentRelationship.Supplement,
            "EncryptedPayload" => PdfAttachmentRelationship.EncryptedPayload,
            "FormData" => PdfAttachmentRelationship.FormData,
            "Schema" => PdfAttachmentRelationship.Schema,
            _ => PdfAttachmentRelationship.Unspecified,
        };
    }

    private void WriteAttachments(List<AttachmentSlot> slots)
    {
        // ISO 32000-1 7.9.6 orders name-tree keys by their bytes and requires one self-consistent encoding, and ISO
        // 32000-2 keeps the rule (PDF Association issue 214). Keys the tree holds keep their bytes. Every other key, for
        // a new attachment or a file listed only in /AF, follows them: UTF-16BE when the tree holds no keys or only
        // UTF-16BE ones, otherwise PDFDocEncoding when the name has a code in it.
        bool utf16 = slots.TrueForAll(slot => !slot.KeyInTree || IsUtf16BeKey(slot.SortKey));
        foreach (AttachmentSlot slot in slots)
        {
            if (!slot.KeyInTree)
            {
                slot.SortKey = utf16 ? PdfTextCodec.EncodeUtf16Be(slot.Name) : PdfTextCodec.EncodePdfDocOrUtf16Be(slot.Name);
            }
        }

        slots.Sort((left, right) => CompareBytes(left.SortKey, right.SortKey));
        var names = new PdfObject.Arr();
        var af = new PdfObject.Arr();
        foreach (AttachmentSlot slot in slots)
        {
            bool hex = false;
            foreach (byte b in slot.SortKey)
            {
                if (b < 32 || b > 126)
                {
                    hex = true;
                    break;
                }
            }

            names.Items.Add(new PdfObject.Str(slot.SortKey, hex));
            names.Items.Add(new PdfObject.Ref(slot.SpecId, 0));
            af.Items.Add(new PdfObject.Ref(slot.SpecId, 0));
        }

        var tree = new PdfObject.Dict();
        tree.Set("Names", names);
        PdfObject.Dict catalog = _store.EditCatalog();
        PdfObject.Dict nameDict;
        if (catalog.Get("Names") is PdfObject.Ref nameRef && _store.IsLive(nameRef) && _store.Resolve(nameRef) is PdfObject.Dict)
        {
            nameDict = (PdfObject.Dict)_store.EditAny(nameRef.ObjectId);
        }
        else if (catalog.Get("Names") is PdfObject.Dict inline)
        {
            nameDict = inline;
        }
        else
        {
            nameDict = new PdfObject.Dict();
            catalog.Set("Names", nameDict);
        }

        nameDict.Set("EmbeddedFiles", tree);
        catalog.Set("AF", af);
        _store.RaiseVersion(14);
    }

    private static string AsciiFallback(string name)
    {
        var chars = new char[name.Length];
        for (int i = 0; i < name.Length; i++)
        {
            chars[i] = name[i] >= 32 && name[i] <= 126 ? name[i] : '_';
        }

        return new string(chars);
    }

    private static bool IsUtf16BeKey(byte[] key)
    {
        return key.Length >= 2 && key[0] == 0xFE && key[1] == 0xFF;
    }

    private static int CompareBytes(byte[] left, byte[] right)
    {
        int count = Math.Min(left.Length, right.Length);
        for (int i = 0; i < count; i++)
        {
            int compare = left[i].CompareTo(right[i]);
            if (compare != 0)
            {
                return compare;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    private sealed class AttachmentSlot
    {
        public AttachmentSlot(string name, byte[] sortKey, int specId, string? description, string? mimeType, long? size, int streamId)
        {
            Name = name;
            SortKey = sortKey;
            SpecId = specId;
            Description = description;
            MimeType = mimeType;
            Size = size;
            StreamId = streamId;
        }

        public string Name { get; set; }

        public byte[] SortKey { get; set; }

        // True when SortKey is a key the /EmbeddedFiles name tree already holds.
        public bool KeyInTree { get; set; }

        public int SpecId { get; }

        public string? Description { get; }

        public string? MimeType { get; }

        public long? Size { get; }

        public int StreamId { get; }

        public PdfAttachmentRelationship Relationship { get; init; }

        // The /Params dates as written, parsed only when PdfAttachment reads them.
        public string? CreationDate { get; init; }

        public string? ModificationDate { get; init; }
    }
}
