#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Internal;

// The document's objects: the source and its cursor, the merged cross-reference map, the object cache and the edits
// over it, the trailer values that root them, and the D-49 operation lease.
internal sealed class PdfObjectStore : IDisposable
{
    internal const int MaxTraversalDepth = 256;

    // PDF versions in tenths, so 17 is PDF 1.7. ISO 32000-1 Table 28 defines the catalog /Version key at PDF 1.4.
    private const int VersionKeyVersion = 14;
    private const int MaxRaisedVersion = 17;

    private readonly Dictionary<int, List<PdfObject>> _objectStreams = new Dictionary<int, List<PdfObject>>();
    private readonly HashSet<int> _loadingObjects = new HashSet<int>();
    private bool _ownsSource;
    private bool _disposed;
    private Dictionary<int, PdfObject?>? _backup;
    private int _objectStreamParseDepth;

    internal PdfObjectStore(IPdfSource source, bool ownsSource, int cursorWindows)
    {
        Source = source;
        _ownsSource = ownsSource;
        Cursor = new PdfCursor(source, cursorWindows);
        Length = source.Length;
    }

    internal Dictionary<int, XrefEntry> Xref { get; } = new Dictionary<int, XrefEntry>();

    internal Dictionary<int, PdfObject> Cache { get; } = new Dictionary<int, PdfObject>();

    internal HashSet<int> Dirty { get; } = new HashSet<int>();

    internal HashSet<long> LoadedXrefs { get; } = new HashSet<long>();

    internal List<RevisionSection> RevisionSections { get; } = new List<RevisionSection>();

    internal List<Stream> OwnedStreams { get; } = new List<Stream>();

    internal IPdfSource Source { get; private set; }

    internal PdfCursor Cursor { get; private set; }

    internal long Length { get; set; }

    internal long StartXref { get; set; }

    // D-56: the section at startxref is a cross-reference stream, so an update appends one too.
    internal bool AppendsXrefStream { get; set; }

    internal int Size { get; set; }

    internal int RootId { get; set; }

    internal int RootGen { get; set; }

    internal int? InfoId { get; set; }

    internal int InfoGen { get; set; }

    internal PdfObject.Dict? InlineInfo { get; set; }

    internal PdfObject.Arr? TrailerId { get; set; }

    internal PdfSecurity? Security { get; set; }

    internal int EncryptId { get; set; }

    internal int EncryptGen { get; set; }

    internal int HeaderVersion { get; set; }

    internal long DecodedBytes { get; set; }

    internal PdfWorkCounters? Work { get; } = PdfWorkCounters.ForNewDocument();

    internal int RetainedObjectStreamCount => _objectStreams.Count;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Security?.Dispose();
        foreach (Stream owned in OwnedStreams)
        {
            owned.Dispose();
        }

        if (_ownsSource)
        {
            Source.Dispose();
        }
    }

    private Stream OpenStored(PdfObject.Dict dict, PdfObject.FileBody file)
    {
        if (Security == null || !Security.EncryptsStream(dict))
        {
            return new PdfSliceStream(Source, file.Offset, file.Length);
        }

        if (Security.UsesInitializationVector(dict))
        {
            if (file.Length < 32)
            {
                throw new PdfParseException(file.Offset, "Encrypted stream is truncated.");
            }

            byte[] initialization = ReadRange(file.Offset, 16);
            var rest = new PdfSliceStream(Source, file.Offset + 16, file.Length - 16);
            return Security.OpenDecrypted(rest, initialization, dict);
        }

        return Security.OpenDecrypted(new PdfSliceStream(Source, file.Offset, file.Length), dict);
    }

    internal PdfObject Load(int id)
    {
        if (Cache.TryGetValue(id, out PdfObject? cached))
        {
            return cached;
        }

        return PdfSync.Result(LoadAsync(id, false, CancellationToken.None));
    }

    // D-55: the one load path. The synchronous Load runs it with useAsync false; opening runs it asynchronously when a
    // cross-reference stream's /Length is indirect.
    private async ValueTask<PdfObject> LoadAsync(int id, bool useAsync, CancellationToken cancellationToken)
    {
        if (Cache.TryGetValue(id, out PdfObject? cached))
        {
            return cached;
        }

        if (!Xref.TryGetValue(id, out XrefEntry entry) || entry.Type == 0)
        {
            throw new PdfParseException(0, "Object " + id.ToString(CultureInfo.InvariantCulture) + " is not in use.");
        }

        if (_loadingObjects.Contains(id))
        {
            throw new PdfParseException(0, "Cyclic indirect object dependency.");
        }

        if (_loadingObjects.Count >= MaxTraversalDepth)
        {
            throw new PdfParseException(0, "Indirect object dependency is too deep.");
        }

        _loadingObjects.Add(id);
        Work?.CountObjectLoad();
        try
        {
            PdfObject obj;
            if (entry.Type == 1)
            {
                Cursor.Seek(entry.Field1);
                var parser = new PdfParser(Cursor, ResolveAsync, 0, useAsync, cancellationToken);
                obj = await parser.ParseIndirectAsync(id).ConfigureAwait(false);
            }
            else if (entry.Type == 2)
            {
                obj = await LoadFromObjectStreamAsync((int)entry.Field1, entry.Field2, id, useAsync, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new PdfParseException(entry.Field1, "Unknown xref entry type.");
            }

            obj.ObjectNumber = id;
            if (entry.Type == 2)
            {
                obj.Generation = 0;
            }

            if (Security != null && entry.Type != 2)
            {
                Security.DecryptStrings(obj);
            }

            Cache[id] = obj;
            return obj;
        }
        finally
        {
            _loadingObjects.Remove(id);
        }
    }

    private async ValueTask<PdfObject> LoadFromObjectStreamAsync(int streamId, int index, int expectedId, bool useAsync, CancellationToken cancellationToken)
    {
        if (!_objectStreams.TryGetValue(streamId, out List<PdfObject>? objects))
        {
            if (!Xref.TryGetValue(streamId, out XrefEntry entry) || entry.Type != 1)
            {
                throw new PdfParseException(0, "Object stream is not an uncompressed object.");
            }

            ReleaseCleanObjectStreams(expectedId, null);
            objects = await ParseObjectStreamAsync(streamId, useAsync, cancellationToken).ConfigureAwait(false);
            _objectStreams[streamId] = objects;
            ReleaseCleanObjectStreams(expectedId, streamId);
        }

        if (index < 0 || index >= objects.Count)
        {
            throw new PdfParseException(0, "Object stream index is outside the object stream.");
        }

        PdfObject obj = objects[index];
        if (obj.ObjectNumber != expectedId)
        {
            throw new PdfParseException(0, "Object stream index " + index.ToString(CultureInfo.InvariantCulture) + " contains object " + obj.ObjectNumber.ToString(CultureInfo.InvariantCulture) + " instead of object " + expectedId.ToString(CultureInfo.InvariantCulture) + ".");
        }

        return obj.DeepClone();
    }

    private async ValueTask<List<PdfObject>> ParseObjectStreamAsync(int streamId, bool useAsync, CancellationToken cancellationToken)
    {
        _objectStreamParseDepth++;
        Work?.CountObjectStreamParse();
        try
        {
            if (await LoadAsync(streamId, useAsync, cancellationToken).ConfigureAwait(false) is not PdfObject.Dict dict)
            {
                throw new PdfParseException(0, "Object stream is not a dictionary.");
            }

            int count = ObjectStreamInteger(dict, "N");
            int first = ObjectStreamInteger(dict, "First");
            if (count < 0 || count > int.MaxValue / 2)
            {
                throw new PdfParseException(0, "Object stream /N is outside the supported range.");
            }

            // Each header pair needs at least "0 0" plus a separator, so /N pairs need 4N - 1 bytes before /First.
            if (count > 0 && ((long)count * 4) - 1 > first)
            {
                throw new PdfParseException(0, "Object stream /N pairs do not fit before /First.");
            }

            byte[] data = await ReadDecodedAsync(dict, PdfStreamDecoder.StructuralCap, useAsync, cancellationToken).ConfigureAwait(false);
            if (first < 0 || first > data.Length)
            {
                throw new PdfParseException(0, "Object stream /First is outside the stream.");
            }

            return PdfObjectStreamReader.Parse(data, count, first);
        }
        finally
        {
            _objectStreamParseDepth--;
        }
    }

    internal ObjectStreamLease ReleaseAfter()
    {
        return new ObjectStreamLease(this);
    }

    // D-49: no source byte read in one public operation is served in another.
    private void EndOperation()
    {
        Cursor.Release();
        ReleaseCleanObjectStreams(null, null);
    }

    private void ReleaseCleanObjectStreams(int? loadingId, int? keepStreamId)
    {
        if (_objectStreamParseDepth > 0 && loadingId == null && keepStreamId == null)
        {
            return;
        }

        if (_objectStreams.Count == 0)
        {
            return;
        }

        List<int>? drop = null;
        foreach (KeyValuePair<int, List<PdfObject>> item in _objectStreams)
        {
            if (keepStreamId != null && item.Key == keepStreamId.Value)
            {
                continue;
            }

            if (ContainsDirtyObject(item.Value))
            {
                continue;
            }

            drop ??= new List<int>();
            drop.Add(item.Key);
        }

        if (drop == null)
        {
            return;
        }

        foreach (int streamId in drop)
        {
            List<PdfObject> objects = _objectStreams[streamId];
            foreach (PdfObject obj in objects)
            {
                int id = obj.ObjectNumber;
                if (KeepCached(id, loadingId))
                {
                    continue;
                }

                if (Cache.Remove(id))
                {
                    Work?.CountCachedObjectEviction();
                }
            }

            _objectStreams.Remove(streamId);
            Work?.CountObjectStreamEviction();
        }
    }

    private bool ContainsDirtyObject(List<PdfObject> objects)
    {
        foreach (PdfObject obj in objects)
        {
            if (Dirty.Contains(obj.ObjectNumber))
            {
                return true;
            }
        }

        return false;
    }

    private bool KeepCached(int id, int? loadingId)
    {
        if (Dirty.Contains(id) || id == RootId || (EncryptId != 0 && id == EncryptId))
        {
            return true;
        }

        if (loadingId != null && id == loadingId.Value)
        {
            return true;
        }

        return InfoId != null && id == InfoId.Value;
    }

    internal PdfObject Resolve(PdfObject value)
    {
        if (value is not PdfObject.Ref)
        {
            return value;
        }

        return PdfSync.Result(ResolveAsync(value, false, CancellationToken.None));
    }

    internal async ValueTask<PdfObject> ResolveAsync(PdfObject value, bool useAsync, CancellationToken cancellationToken)
    {
        // W-115: nearly every value is direct or one reference from one, so the set is made at a chain's second reference.
        bool followed = false;
        int firstId = 0;
        HashSet<int>? seen = null;
        while (value is PdfObject.Ref reference)
        {
            Work?.CountReferenceResolution();
            if (!followed)
            {
                followed = true;
                firstId = reference.ObjectId;
            }
            else
            {
                seen ??= new HashSet<int> { firstId };
                if (!seen.Add(reference.ObjectId))
                {
                    throw new PdfParseException(0, "Cyclic indirect object reference.");
                }

                if (seen.Count > MaxTraversalDepth)
                {
                    throw new PdfParseException(0, "Indirect object reference chain is too deep.");
                }
            }

            if (!IsLive(reference))
            {
                return PdfObject.NullObj.Instance;
            }

            value = await LoadAsync(reference.ObjectId, useAsync, cancellationToken).ConfigureAwait(false);
        }

        return value;
    }

    // ISO 32000-1 7.3.10 and 7.5.8.3: a reference to an undefined or free object, to an entry of an unknown type, or
    // with a generation other than the entry's is a reference to the null object. An object-stream member has generation 0.
    internal bool IsLive(PdfObject.Ref reference)
    {
        if (!Xref.TryGetValue(reference.ObjectId, out XrefEntry entry))
        {
            return false;
        }

        return entry.Type switch
        {
            1 => entry.Field2 == reference.GenerationNumber,
            2 => reference.GenerationNumber == 0,
            _ => false,
        };
    }

    internal bool IsDangling(PdfObject? value)
    {
        return value is PdfObject.Ref reference && !IsLive(reference);
    }

    internal PdfObject? ResolveMaybe(PdfObject? value)
    {
        return value == null ? null : Resolve(value);
    }

    internal PdfObject EditAny(int id)
    {
        Backup(id);
        if (Dirty.Contains(id) && Cache.TryGetValue(id, out PdfObject? edited))
        {
            return edited;
        }

        PdfObject source = Load(id);
        PdfObject clone = source.DeepClone();
        clone.ObjectNumber = source.ObjectNumber;
        clone.Generation = source.Generation;
        Cache[id] = clone;
        Dirty.Add(id);
        return clone;
    }

    internal int Allocate(PdfObject obj)
    {
        int id = Size++;
        Backup(id);
        obj.ObjectNumber = id;
        obj.Generation = 0;
        Cache[id] = obj;
        Dirty.Add(id);
        Xref[id] = new XrefEntry(1, 0, 0);
        return id;
    }

    private void Backup(int id)
    {
        if (_backup == null || _backup.ContainsKey(id))
        {
            return;
        }

        if (Cache.TryGetValue(id, out PdfObject? cached))
        {
            _backup[id] = cached.DeepClone();
            return;
        }

        if (Xref.ContainsKey(id))
        {
            _backup[id] = Load(id).DeepClone();
            return;
        }

        _backup[id] = null;
    }

    internal void Restore(int size, int? infoId, int infoGen, PdfObject.Arr? id, HashSet<int> dirty)
    {
        if (_backup != null)
        {
            foreach (KeyValuePair<int, PdfObject?> item in _backup)
            {
                if (item.Value == null)
                {
                    Cache.Remove(item.Key);
                    Xref.Remove(item.Key);
                }
                else
                {
                    Cache[item.Key] = item.Value;
                }
            }
        }

        _backup = null;
        Size = size;
        InfoId = infoId;
        InfoGen = infoGen;
        TrailerId = id;
        Dirty.Clear();
        foreach (int dirtyId in dirty)
        {
            Dirty.Add(dirtyId);
        }
    }

    internal PdfObject.Dict CatalogDict()
    {
        if (Resolve(new PdfObject.Ref(RootId, RootGen)) is not PdfObject.Dict dict)
        {
            throw new PdfParseException(0, "The catalog is not a dictionary.");
        }

        return dict;
    }

    internal PdfObject.Dict EditCatalog()
    {
        CatalogDict();
        return (PdfObject.Dict)EditAny(RootId);
    }

    // Spec 7.5: a write that emits a key ISO 32000-1 defines at a later version than the effective version raises the
    // catalog /Version in the same revision, to at least the version of /Version itself and never past 1.7. An
    // effective 1.7 or 2.0 is left alone. The header is never rewritten.
    internal void RaiseVersion(int required)
    {
        int effective = EffectiveVersion();
        int target = Math.Min(Math.Max(required, VersionKeyVersion), MaxRaisedVersion);
        if (required <= effective || target <= effective)
        {
            return;
        }

        EditCatalog().Set("Version", new PdfObject.Name("1." + (char)('0' + (target - 10))));
    }

    // ISO 32000-1 7.5.2 and 7.7.2: a catalog /Version later than the header is the version. A value that is not a
    // version name, such as a string, declares nothing.
    private int EffectiveVersion()
    {
        if (ResolveMaybe(CatalogDict().Get("Version")) is PdfObject.Name name
            && name.Value.Length == 3
            && name.Value[0] >= '1' && name.Value[0] <= '9'
            && name.Value[1] == '.'
            && name.Value[2] >= '0' && name.Value[2] <= '9')
        {
            return Math.Max(HeaderVersion, ((name.Value[0] - '0') * 10) + (name.Value[2] - '0'));
        }

        return HeaderVersion;
    }

    internal bool TryNumber(PdfObject? value, out double number)
    {
        if (ResolveMaybe(value) is PdfObject.Num numeric)
        {
            number = numeric.IsInteger ? numeric.LongValue : numeric.DoubleValue;
            return true;
        }

        number = 0;
        return false;
    }

    internal PdfObject.Arr EditArray(PdfObject.Dict parent, string key)
    {
        if (parent.Get(key) is PdfObject.Ref reference && IsLive(reference))
        {
            return (PdfObject.Arr)EditAny(reference.ObjectId);
        }

        if (parent.Get(key) is PdfObject.Arr array)
        {
            return array;
        }

        var created = new PdfObject.Arr();
        parent.Set(key, created);
        return created;
    }

    // The destination becomes the document's source, a new snapshot the document owns; leaveOpen keeps its stream open.
    internal void SwitchStream(Stream destination, bool leaveOpen)
    {
        var source = new PdfStreamSource(destination, leaveOpen);
        var cursor = new PdfCursor(source, Cursor.RetainedWindows);
        if (_ownsSource)
        {
            Source.Dispose();
        }

        Source = source;
        _ownsSource = true;
        Length = source.Length;
        Cursor = cursor;
    }

    internal Stream OpenBody(PdfObject.Dict dict)
    {
        if (dict.Body is PdfObject.MemoryBody memory)
        {
            return new MemoryStream(memory.Data, false);
        }

        if (dict.Body is PdfObject.ExternalBody external)
        {
            return new PdfSliceStream(PdfStreamSource.Borrow(external.Source), external.Start, external.ContentLength);
        }

        if (dict.Body is not PdfObject.FileBody file)
        {
            throw new PdfParseException(0, "The stream has no bytes.");
        }

        List<string> filters = PdfStreamDecoder.FiltersOf(dict, Resolve);
        List<PdfObject.Dict?> parms = PdfStreamDecoder.ParmsOf(dict, filters.Count, Resolve);
        return PdfStreamDecoder.OpenDecoded(OpenStored(dict, file), filters, parms);
    }

    // The new-file copier preserves the encoded stream and removes only standard-security encryption. Structural
    // streams still use OpenBody or ReadDecoded because their filters must be interpreted.
    internal Stream OpenRawBody(PdfObject.Dict dict)
    {
        if (dict.Body is PdfObject.MemoryBody memory)
        {
            return new MemoryStream(memory.Data, false);
        }

        if (dict.Body is PdfObject.ExternalBody external)
        {
            return new PdfSliceStream(PdfStreamSource.Borrow(external.Source), external.Start, external.Length);
        }

        if (dict.Body is not PdfObject.FileBody file)
        {
            throw new PdfParseException(0, "The stream has no bytes.");
        }

        return OpenStored(dict, file);
    }

    internal byte[] ReadDecoded(PdfObject.Dict dict, int cap)
    {
        return PdfSync.Result(ReadDecodedAsync(dict, cap, false, CancellationToken.None));
    }

    private async ValueTask<byte[]> ReadDecodedAsync(PdfObject.Dict dict, int cap, bool useAsync, CancellationToken cancellationToken)
    {
        if (dict.Body is PdfObject.MemoryBody memory)
        {
            return memory.Data;
        }

        if (dict.Body is not PdfObject.FileBody file)
        {
            throw new PdfParseException(0, "The stream has no bytes.");
        }

        if (file.Length > cap)
        {
            throw new PdfObjectTooLargeException("The stream is larger than " + cap.ToString(CultureInfo.InvariantCulture) + " bytes.");
        }

        byte[] raw = await ReadRangeAsync(file.Offset, (int)file.Length, useAsync, cancellationToken).ConfigureAwait(false);
        if (Security != null && Security.EncryptsStream(dict))
        {
            raw = Security.Decrypt(raw, dict);
        }

        if (useAsync)
        {
            await PreloadFiltersAsync(dict, cancellationToken).ConfigureAwait(false);
        }

        List<string> filters = PdfStreamDecoder.FiltersOf(dict, Resolve);
        if (filters.Count == 0)
        {
            return raw;
        }

        if (useAsync)
        {
            await PreloadDecodeParmsAsync(dict, cancellationToken).ConfigureAwait(false);
        }

        byte[] decoded = PdfStreamDecoder.Decode(raw, filters, PdfStreamDecoder.ParmsOf(dict, filters.Count, Resolve), cap, Work);
        DecodedBytes += decoded.Length;
        return decoded;
    }

    // D-55: the decoder resolves /Filter and its entries synchronously, so an asynchronous read loads them first and the
    // decoder's resolver then finds them cached.
    private async ValueTask PreloadFiltersAsync(PdfObject.Dict dict, CancellationToken cancellationToken)
    {
        if (await ResolvePresentAsync(dict.Get("Filter"), cancellationToken).ConfigureAwait(false) is PdfObject.Arr filters)
        {
            foreach (PdfObject item in filters.Items)
            {
                await ResolveAsync(item, true, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // The same for /DecodeParms, its entries, and the parameter values the decoder reads.
    private async ValueTask PreloadDecodeParmsAsync(PdfObject.Dict dict, CancellationToken cancellationToken)
    {
        PdfObject? parms = await ResolvePresentAsync(dict.Get("DecodeParms"), cancellationToken).ConfigureAwait(false);
        if (parms is PdfObject.Arr array)
        {
            foreach (PdfObject item in array.Items)
            {
                if (await ResolveAsync(item, true, cancellationToken).ConfigureAwait(false) is PdfObject.Dict entry)
                {
                    await PreloadParametersAsync(entry, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        else if (parms is PdfObject.Dict one)
        {
            await PreloadParametersAsync(one, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask PreloadParametersAsync(PdfObject.Dict parms, CancellationToken cancellationToken)
    {
        foreach (string key in PdfStreamDecoder.DecoderParameters)
        {
            await ResolvePresentAsync(parms.Get(key), cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<PdfObject?> ResolvePresentAsync(PdfObject? value, CancellationToken cancellationToken)
    {
        return value == null ? null : await ResolveAsync(value, true, cancellationToken).ConfigureAwait(false);
    }

    internal byte[] ReadRange(long offset, int count)
    {
        return PdfSync.Result(ReadRangeAsync(offset, count, false, CancellationToken.None));
    }

    // A source read may be partial, so the range is read until it is whole or the source ends.
    internal async ValueTask<byte[]> ReadRangeAsync(long offset, int count, bool useAsync, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        int readTotal = 0;
        while (readTotal < count)
        {
            int chunk = Math.Min(65536, count - readTotal);
            int read = useAsync
                ? await Source.ReadAsync(offset + readTotal, new Memory<byte>(buffer, readTotal, chunk), cancellationToken).ConfigureAwait(false)
                : Source.Read(offset + readTotal, new Span<byte>(buffer, readTotal, chunk));
            if (read == 0)
            {
                throw new PdfParseException(offset, "Unexpected end of file.");
            }

            readTotal += read;
        }

        return buffer;
    }

    internal void CheckDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PdfDocument));
        }
    }

    internal string? ValueText(PdfObject? value)
    {
        value = ResolveMaybe(value);
        if (value is PdfObject.Name name)
        {
            return name.Value;
        }

        if (value is PdfObject.Str str)
        {
            return PdfTextCodec.Decode(str.Raw);
        }

        if (value is PdfObject.Arr array && array.Items.Count > 0)
        {
            return ValueText(array.Items[0]);
        }

        return null;
    }

    private static int ObjectStreamInteger(PdfObject.Dict dict, string key)
    {
        if (dict.Get(key) is not PdfObject.Num number || !number.IsInteger || number.LongValue < 0 || number.LongValue > int.MaxValue)
        {
            throw new PdfParseException(0, "Object stream /" + key + " is not a non-negative 32-bit integer.");
        }

        return (int)number.LongValue;
    }

    internal PdfRect? RectOf(PdfObject? value)
    {
        if (ResolveMaybe(value) is not PdfObject.Arr array || array.Items.Count < 4)
        {
            return null;
        }

        return new PdfRect(DoubleOf(array.Items[0]), DoubleOf(array.Items[1]), DoubleOf(array.Items[2]), DoubleOf(array.Items[3]));
    }

    private double DoubleOf(PdfObject value)
    {
        value = Resolve(value);
        if (value is PdfObject.Num number)
        {
            return number.IsInteger ? number.LongValue : number.DoubleValue;
        }

        return 0;
    }

    internal readonly struct ObjectStreamLease : IDisposable
    {
        private readonly PdfObjectStore _store;

        public ObjectStreamLease(PdfObjectStore store)
        {
            _store = store;
        }

        public void Dispose()
        {
            _store.EndOperation();
        }
    }

    // Fill, flatten, and sign record each object's state before its first edit, so a failure restores it.
    internal void BeginBackup()
    {
        _backup = new Dictionary<int, PdfObject?>();
    }

    internal void EndBackup()
    {
        _backup = null;
    }
}
