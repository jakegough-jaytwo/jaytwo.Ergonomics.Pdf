#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace jaytwo.Pdf.Internal;

// Appends a revision, the dirty objects, and a cross-reference section in the format of the one at startxref (D-56),
// to the source or to a copy of it in an empty destination.
internal sealed class PdfIncrementalWriter
{
    internal const long MaxClassicOffset = 9999999999L;

    private readonly PdfObjectStore _store;
    private readonly PdfWriteGate _gate;

    internal PdfIncrementalWriter(PdfObjectStore store, PdfWriteGate gate)
    {
        _store = store;
        _gate = gate;
    }

    // The synchronous and asynchronous saves share this path, so their bytes are identical. With useAsync false
    // every await completes synchronously. The serializer writes the appendix to the destination synchronously.
    internal async Task SaveIncrementalAsync(Stream destination, bool leaveOpen, bool useAsync, CancellationToken cancellationToken)
    {
        _store.CheckDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        using var release = _store.ReleaseAfter();
        ValidateDestination(destination);
        if (_store.Length > MaxClassicOffset)
        {
            throw new PdfTooLargeForClassicXrefException();
        }

        bool same = IsSourceStream(destination);
        if (_store.Dirty.Count == 0)
        {
            if (!same)
            {
                try
                {
                    await CopyPrefixAsync(destination, useAsync, cancellationToken).ConfigureAwait(false);
                    _store.SwitchStream(destination, leaveOpen);
                }
                catch
                {
                    Truncate(destination, 0);
                    throw;
                }
            }

            return;
        }

        _gate.EnsureAppendAllowed();
        AppendixLayout layout;
        using var appending = BeginAppend(same);
        try
        {
            if (!same)
            {
                await CopyPrefixAsync(destination, useAsync, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                destination.Seek(_store.Length, SeekOrigin.Begin);
            }

            if (NeedsLeadingLineFeed())
            {
                destination.WriteByte((byte)'\n');
            }

            layout = WriteAppendix(destination, 0);
        }
        catch
        {
            Truncate(destination, same ? _store.Length : 0);
            throw;
        }

        FinishWrite(destination, leaveOpen, same, layout);
    }

    internal AppendixLayout WriteAppendix(Stream output, long origin)
    {
        var layout = new AppendixLayout();
        var serializer = new PdfSerializer(output, CopyFileBody, _store.Security);
        foreach (int id in _store.Dirty.OrderBy(static value => value))
        {
            PdfObject obj = _store.Cache[id];
            long absolute = origin + output.Position;
            if (absolute > MaxClassicOffset)
            {
                throw new PdfTooLargeForClassicXrefException();
            }

            layout.Objects.Add(new WrittenObject(id, absolute, obj.Generation));
            serializer.WriteObject(obj);
        }

        layout.XrefOffset = origin + output.Position;
        if (layout.XrefOffset > MaxClassicOffset)
        {
            throw new PdfTooLargeForClassicXrefException();
        }

        if (_store.AppendsXrefStream)
        {
            WriteXrefStream(output, layout, serializer);
        }
        else
        {
            WriteClassicXref(output, layout, serializer);
        }

        WriteAscii(output, "startxref\n" + layout.XrefOffset.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
        layout.ByteRange = serializer.ByteRange;
        layout.Contents = serializer.Contents;
        return layout;
    }

    private void WriteClassicXref(Stream output, AppendixLayout layout, PdfSerializer serializer)
    {
        layout.Size = _store.Size;
        WriteAscii(output, "xref\r\n");
        List<WrittenObject> items = layout.Objects.OrderBy(item => item.Id).ToList();
        int index = 0;
        while (index < items.Count)
        {
            int start = items[index].Id;
            int count = Run(items, index);
            WriteAscii(output, start.ToString(CultureInfo.InvariantCulture) + " " + count.ToString(CultureInfo.InvariantCulture) + "\r\n");
            for (int i = 0; i < count; i++)
            {
                WrittenObject item = items[index + i];
                string offsetText = item.Offset.ToString("D10", CultureInfo.InvariantCulture);
                string generationText = item.Generation.ToString("D5", CultureInfo.InvariantCulture);
                if (offsetText.Length != 10 || generationText.Length != 5)
                {
                    throw new PdfTooLargeForClassicXrefException();
                }

                WriteAscii(output, offsetText + " " + generationText + " n\r\n");
            }

            index += count;
        }

        WriteAscii(output, "trailer\n<<\n");
        WriteTrailerEntries(output, layout, serializer);
        WriteAscii(output, ">>\n");
    }

    // D-56: a cross-reference stream (ISO 32000-1 7.5.8) after a source whose section at startxref is one. It takes the
    // next free object number and lists itself. The trailer entries go in its dictionary, which is never encrypted, and
    // its rows are type 1 entries whose big-endian offset field is as wide as the largest offset, its own.
    private void WriteXrefStream(Stream output, AppendixLayout layout, PdfSerializer serializer)
    {
        if (_store.Size == int.MaxValue)
        {
            throw new PdfException("The file has no free object number for a cross-reference stream.");
        }

        int streamId = _store.Size;
        layout.Objects.Add(new WrittenObject(streamId, layout.XrefOffset, 0));
        layout.Size = streamId + 1;
        List<WrittenObject> items = layout.Objects.OrderBy(item => item.Id).ToList();
        int offsetWidth = ByteWidth(layout.XrefOffset);
        int generationWidth = Math.Max(2, ByteWidth(items.Max(item => item.Generation)));
        var row = new byte[1 + offsetWidth + generationWidth];
        using var rows = new MemoryStream(items.Count * row.Length);
        foreach (WrittenObject item in items)
        {
            row[0] = 1;
            WriteBigEndian(row, 1, offsetWidth, item.Offset);
            WriteBigEndian(row, 1 + offsetWidth, generationWidth, item.Generation);
            rows.Write(row, 0, row.Length);
        }

        using var compressed = new MemoryStream();
        PdfFlateEncoder.Copy(rows, 0, rows.Length, compressed, true);
        var index = new StringBuilder();
        for (int i = 0; i < items.Count;)
        {
            int count = Run(items, i);
            index.Append(i == 0 ? string.Empty : " ")
                .Append(items[i].Id.ToString(CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(count.ToString(CultureInfo.InvariantCulture));
            i += count;
        }

        WriteAscii(output, streamId.ToString(CultureInfo.InvariantCulture) + " 0 obj\n<<\n/Type /XRef\n");
        WriteTrailerEntries(output, layout, serializer);
        WriteAscii(output, "/W [1 " + offsetWidth.ToString(CultureInfo.InvariantCulture) + " " + generationWidth.ToString(CultureInfo.InvariantCulture) + "]\n/Index [" + index + "]\n/Filter /FlateDecode\n/Length " + compressed.Length.ToString(CultureInfo.InvariantCulture) + "\n>>\nstream\n");
        compressed.WriteTo(output);
        WriteAscii(output, "\nendstream\nendobj\n");
    }

    private void WriteTrailerEntries(Stream output, AppendixLayout layout, PdfSerializer serializer)
    {
        WriteAscii(output, "/Size " + layout.Size.ToString(CultureInfo.InvariantCulture) + "\n/Prev " + _store.StartXref.ToString(CultureInfo.InvariantCulture) + "\n/Root " + _store.RootId.ToString(CultureInfo.InvariantCulture) + " " + _store.RootGen.ToString(CultureInfo.InvariantCulture) + " R\n");
        if (_store.InfoId != null)
        {
            WriteAscii(output, "/Info " + _store.InfoId.Value.ToString(CultureInfo.InvariantCulture) + " " + _store.InfoGen.ToString(CultureInfo.InvariantCulture) + " R\n");
        }

        if (_store.EncryptId != 0)
        {
            WriteAscii(output, "/Encrypt " + _store.EncryptId.ToString(CultureInfo.InvariantCulture) + " " + _store.EncryptGen.ToString(CultureInfo.InvariantCulture) + " R\n");
        }

        if (_store.TrailerId != null)
        {
            WriteAscii(output, "/ID ");
            serializer.WriteValue(_store.TrailerId);
            WriteAscii(output, "\n");
        }
    }

    // The number of consecutive object numbers from items[index], which are sorted by number: one subsection.
    private static int Run(List<WrittenObject> items, int index)
    {
        int start = items[index].Id;
        int count = 1;
        while (index + count < items.Count && items[index + count].Id == start + count)
        {
            count++;
        }

        return count;
    }

    private static int ByteWidth(long value)
    {
        int width = 1;
        while (width < 8 && (value >> (8 * width)) != 0)
        {
            width++;
        }

        return width;
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int width, long value)
    {
        for (int i = width - 1; i >= 0; i--)
        {
            buffer[offset + i] = (byte)value;
            value >>= 8;
        }
    }

    // The steps that can fail (the destination length and releasing an owned source) run before the engine records the
    // revision, so a failure truncates the destination and leaves the pending edits for a retry.
    internal void FinishWrite(Stream destination, bool leaveOpen, bool same, AppendixLayout layout)
    {
        try
        {
            if (same)
            {
                var source = (PdfStreamSource)_store.Source;
                source.AcceptAppend();
                _store.Length = source.Length;
            }
            else
            {
                _store.SwitchStream(destination, leaveOpen);
            }
        }
        catch
        {
            Truncate(destination, same ? _store.Length : 0);
            throw;
        }

        foreach (WrittenObject item in layout.Objects)
        {
            _store.Xref[item.Id] = new XrefEntry(1, item.Offset, item.Generation);
        }

        _store.StartXref = layout.XrefOffset;
        if (layout.Size > _store.Size)
        {
            _store.Size = layout.Size;
        }

        _store.Dirty.Clear();
    }

    internal bool IsSourceStream(Stream destination)
    {
        return _store.Source is PdfStreamSource source && ReferenceEquals(destination, source.Stream);
    }

    // A save or signature with no destination appends to the stream the document was opened from.
    internal Stream SourceStream()
    {
        _store.CheckDisposed();
        return _store.Source is PdfStreamSource source
            ? source.Stream
            : throw new InvalidOperationException("A document opened from an IPdfSource has no stream to append to. Save to a destination stream.");
    }

    // While the document appends to its own source, reads of the prefix accept the longer stream.
    internal AppendScope BeginAppend(bool same)
    {
        return new AppendScope(same ? (PdfStreamSource)_store.Source : null);
    }

    internal void ValidateDestination(Stream destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (!destination.CanWrite || !destination.CanSeek)
        {
            throw new ArgumentException("The destination must be a writable seekable stream.", nameof(destination));
        }

        if (IsSourceStream(destination))
        {
            ((PdfStreamSource)_store.Source).CheckUnchanged();
            return;
        }

        if (destination.Length != 0)
        {
            throw new PdfException("The destination stream must be empty.");
        }
    }

    internal async Task CopyPrefixAsync(Stream destination, bool useAsync, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 1024];
        long remaining = _store.Length;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = (int)Math.Min(buffer.Length, remaining);
            long position = _store.Length - remaining;
            int read = useAsync
                ? await _store.Source.ReadAsync(position, new Memory<byte>(buffer, 0, count), cancellationToken).ConfigureAwait(false)
                : _store.Source.Read(position, new Span<byte>(buffer, 0, count));
            if (read == 0)
            {
                throw new EndOfStreamException("The PDF stream ended before its declared length.");
            }

            if (useAsync)
            {
                await destination.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                destination.Write(buffer, 0, read);
            }

            remaining -= read;
        }
    }

    // A failed or canceled write returns the destination to the length it had: empty, or the opened file's length.
    // The write's own exception is the one the caller needs, so a failure to truncate is not reported over it.
    internal static void Truncate(Stream destination, long length)
    {
        try
        {
            destination.SetLength(length);
            destination.Seek(length, SeekOrigin.Begin);
        }
        catch (Exception)
        {
        }
    }

    private void CopyFileBody(PdfObject.FileBody body, Stream destination)
    {
        long writeAt = destination.Position;
        long readAt = body.Offset;
        long remaining = body.Length;
        var buffer = new byte[65536];
        while (remaining > 0)
        {
            int count = (int)Math.Min(buffer.Length, remaining);
            int read = _store.Source.Read(readAt, new Span<byte>(buffer, 0, count));
            if (read == 0)
            {
                throw new EndOfStreamException("A PDF stream object ended early.");
            }

            destination.Seek(writeAt, SeekOrigin.Begin);
            destination.Write(buffer, 0, read);
            readAt += read;
            writeAt += read;
            remaining -= read;
        }

        destination.Seek(writeAt, SeekOrigin.Begin);
    }

    internal bool NeedsLeadingLineFeed()
    {
        if (_store.Length == 0)
        {
            return false;
        }

        var last = new byte[1];
        return _store.Source.Read(_store.Length - 1, last) == 0 || last[0] != '\n';
    }

    internal Stream OpenSigned(MemoryStream appendix, long lessThan, long gap)
    {
        return new SplicedStream(new[]
        {
            new SplicedStream.Piece(new PdfSliceStream(_store.Source, 0, _store.Length), 0, _store.Length),
            new SplicedStream.Piece(appendix, 0, lessThan),
            new SplicedStream.Piece(appendix, lessThan + gap, appendix.Length - lessThan - gap),
        });
    }

    private static void WriteAscii(Stream output, string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        output.Write(bytes, 0, bytes.Length);
    }

    internal readonly struct AppendScope : IDisposable
    {
        private readonly PdfStreamSource? _source;

        public AppendScope(PdfStreamSource? source)
        {
            _source = source;
            if (source != null)
            {
                source.Appending = true;
            }
        }

        public void Dispose()
        {
            if (_source != null)
            {
                _source.Appending = false;
            }
        }
    }
}
