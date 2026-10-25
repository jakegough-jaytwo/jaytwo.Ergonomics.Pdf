#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace jaytwo.Pdf.Internal;

// Writes a new PDF from registered destination objects. Unlike the incremental writer, it only counts bytes and never
// seeks. Imported stream lengths are emitted as indirect objects immediately after their streams.
internal sealed class PdfNewFileWriter
{
    internal const int PageTreeFanout = 64;
    internal const long MaxClassicOffset = 9999999999L;

    private readonly List<PdfObject> _objects = new List<PdfObject>();
    private readonly PdfNewFileBudget _budget;
    private int _nextObjectNumber = 1;

    internal PdfNewFileWriter(PdfNewFileBudget budget)
    {
        _budget = budget ?? throw new ArgumentNullException(nameof(budget));
    }

    internal PdfObject.Ref Register(PdfObject value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        int id = NextObjectNumber();
        value.ObjectNumber = id;
        value.Generation = 0;
        _objects.Add(value);
        _budget.AddStructuralBytes(24);
        return new PdfObject.Ref(id, 0);
    }

    internal PdfObject.Ref BuildPageTree(IReadOnlyList<PdfObject.Ref> pages)
    {
        if (pages == null)
        {
            throw new ArgumentNullException(nameof(pages));
        }

        _budget.CountOutputPages(pages.Count);
        var level = new List<PageTreeEntry>(pages.Count);
        foreach (PdfObject.Ref page in pages)
        {
            level.Add(new PageTreeEntry(page, 1));
        }

        // Even an empty result gets a valid root /Pages node.
        do
        {
            var parents = new List<PageTreeEntry>((level.Count + PageTreeFanout - 1) / PageTreeFanout);
            if (level.Count == 0)
            {
                parents.Add(CreatePageTreeNode(level, 0, 0));
            }
            else
            {
                for (int start = 0; start < level.Count; start += PageTreeFanout)
                {
                    parents.Add(CreatePageTreeNode(level, start, Math.Min(PageTreeFanout, level.Count - start)));
                }
            }

            level = parents;
        }
        while (level.Count > 1);

        return level[0].Reference;
    }

    internal void Write(Stream destination, PdfObject.Ref root, PdfObject.Ref? info, PdfObject.Arr identifier, int effectiveVersion)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (!destination.CanWrite)
        {
            throw new ArgumentException("The destination must be writable.", nameof(destination));
        }

        if (identifier == null)
        {
            throw new ArgumentNullException(nameof(identifier));
        }

        int version = Math.Max(15, effectiveVersion);
        using var output = new CountingWriteStream(destination);
        WriteAscii(output, "%PDF-" + VersionText(version) + "\n%\xE2\xE3\xCF\xD3\n");

        var offsets = new Dictionary<int, long>();
        var serializer = new PdfSerializer(output, static (_, _) => throw new InvalidOperationException("A source file body must be imported before a new file is written."), null, preserveStreamLength: true);
        int registered = _objects.Count;
        for (int i = 0; i < registered; i++)
        {
            PdfObject value = _objects[i];
            offsets.Add(value.ObjectNumber, output.Count);
            if (value is PdfObject.Dict dict && dict.Body is PdfObject.CopiedBody copied)
            {
                int lengthId = NextObjectNumber();
                dict.Set("Length", new PdfObject.Ref(lengthId, 0));
                serializer.WriteObject(dict);
                if (copied.CopiedLength < 0)
                {
                    throw new PdfException("An imported stream did not report its copied length.");
                }

                offsets.Add(lengthId, output.Count);
                WriteNumberObject(serializer, lengthId, copied.CopiedLength);
            }
            else if (value is PdfObject.Dict knownStream && knownStream.Body != null)
            {
                knownStream.Set("Length", PdfObject.Num.FromLong(knownStream.Body.Length));
                serializer.WriteObject(knownStream);
            }
            else
            {
                serializer.WriteObject(value);
            }
        }

        long xrefOffset = output.Count;
        long maximumOffset = offsets.Count == 0 ? xrefOffset : Math.Max(xrefOffset, offsets.Values.Max());
        if (UsesClassicXref(maximumOffset))
        {
            WriteClassicXref(output, offsets, root, info, identifier);
        }
        else
        {
            WriteXrefStream(output, offsets, root, info, identifier, xrefOffset);
        }

        WriteAscii(output, "startxref\n" + xrefOffset.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
    }

    internal static bool UsesClassicXref(long maximumOffset)
    {
        return maximumOffset >= 0 && maximumOffset <= MaxClassicOffset;
    }

    private PageTreeEntry CreatePageTreeNode(List<PageTreeEntry> level, int start, int count)
    {
        var kids = new PdfObject.Arr(count);
        int descendantPages = 0;
        for (int i = 0; i < count; i++)
        {
            PageTreeEntry child = level[start + i];
            kids.Items.Add(child.Reference);
            descendantPages = checked(descendantPages + child.PageCount);
        }

        var node = new PdfObject.Dict(3);
        node.Append("Type", new PdfObject.Name("Pages"));
        node.Append("Kids", kids);
        node.Append("Count", PdfObject.Num.FromLong(descendantPages));
        PdfObject.Ref nodeReference = Register(node);
        for (int i = 0; i < count; i++)
        {
            PdfObject child = ObjectFor(level[start + i].Reference);
            if (child is not PdfObject.Dict childDictionary)
            {
                throw new PdfException("A page-tree child is not a dictionary.");
            }

            childDictionary.Set("Parent", nodeReference);
        }

        return new PageTreeEntry(nodeReference, descendantPages);
    }

    private PdfObject ObjectFor(PdfObject.Ref reference)
    {
        int index = reference.ObjectId - 1;
        if (index < 0 || index >= _objects.Count)
        {
            throw new PdfException("A page-tree reference does not name a registered object.");
        }

        return _objects[index];
    }

    private int NextObjectNumber()
    {
        if (_nextObjectNumber == int.MaxValue)
        {
            throw new PdfResourceLimitException("mapped objects");
        }

        return _nextObjectNumber++;
    }

    private void WriteClassicXref(Stream output, Dictionary<int, long> offsets, PdfObject.Ref root, PdfObject.Ref? info, PdfObject.Arr identifier)
    {
        int size = _nextObjectNumber;
        WriteAscii(output, "xref\r\n0 " + size.ToString(CultureInfo.InvariantCulture) + "\r\n");
        WriteAscii(output, "0000000000 65535 f\r\n");
        for (int id = 1; id < size; id++)
        {
            if (!offsets.TryGetValue(id, out long offset))
            {
                throw new PdfException("A registered new-file object was not written.");
            }

            WriteAscii(output, offset.ToString("D10", CultureInfo.InvariantCulture) + " 00000 n\r\n");
        }

        WriteAscii(output, "trailer\n<<\n");
        WriteTrailer(output, size, root, info, identifier);
        WriteAscii(output, ">>\n");
    }

    private void WriteXrefStream(Stream output, Dictionary<int, long> offsets, PdfObject.Ref root, PdfObject.Ref? info, PdfObject.Arr identifier, long xrefOffset)
    {
        int xrefId = NextObjectNumber();
        offsets.Add(xrefId, xrefOffset);
        int size = _nextObjectNumber;
        int offsetWidth = ByteWidth(xrefOffset);
        int rowWidth = 1 + offsetWidth + 2;
        long length = checked((long)size * rowWidth);

        WriteAscii(output, xrefId.ToString(CultureInfo.InvariantCulture) + " 0 obj\n<<\n/Type /XRef\n");
        WriteTrailer(output, size, root, info, identifier);
        WriteAscii(output, "/W [1 " + offsetWidth.ToString(CultureInfo.InvariantCulture) + " 2]\n/Length " + length.ToString(CultureInfo.InvariantCulture) + "\n>>\nstream\n");
        var row = new byte[rowWidth];
        row[0] = 0;
        row[rowWidth - 2] = 255;
        row[rowWidth - 1] = 255;
        output.Write(row, 0, row.Length);
        for (int id = 1; id < size; id++)
        {
            Array.Clear(row, 0, row.Length);
            row[0] = 1;
            WriteBigEndian(row, 1, offsetWidth, offsets[id]);
            output.Write(row, 0, row.Length);
        }

        WriteAscii(output, "\nendstream\nendobj\n");
    }

    private static void WriteTrailer(Stream output, int size, PdfObject.Ref root, PdfObject.Ref? info, PdfObject.Arr identifier)
    {
        WriteAscii(output, "/Size " + size.ToString(CultureInfo.InvariantCulture) + "\n/Root " + ReferenceText(root) + "\n");
        if (info != null)
        {
            WriteAscii(output, "/Info " + ReferenceText(info) + "\n");
        }

        WriteAscii(output, "/ID ");
        var serializer = new PdfSerializer(output, static (_, _) => throw new InvalidOperationException(), null, preserveStreamLength: true);
        serializer.WriteValue(identifier);
        WriteAscii(output, "\n");
    }

    private static void WriteNumberObject(PdfSerializer serializer, int id, long value)
    {
        PdfObject.Num length = PdfObject.Num.FromLong(value);
        length.ObjectNumber = id;
        serializer.WriteObject(length);
    }

    private static int ByteWidth(long value)
    {
        int width = 1;
        while (width < 8 && (value >> (width * 8)) != 0)
        {
            width++;
        }

        return width;
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int width, long value)
    {
        for (int index = width - 1; index >= 0; index--)
        {
            buffer[offset + index] = (byte)value;
            value >>= 8;
        }
    }

    private static string ReferenceText(PdfObject.Ref reference)
    {
        return reference.ObjectId.ToString(CultureInfo.InvariantCulture) + " " + reference.GenerationNumber.ToString(CultureInfo.InvariantCulture) + " R";
    }

    private static string VersionText(int version)
    {
        int major = version / 10;
        int minor = version % 10;
        return major.ToString(CultureInfo.InvariantCulture) + "." + minor.ToString(CultureInfo.InvariantCulture);
    }

    private static void WriteAscii(Stream output, string value)
    {
        var bytes = new byte[value.Length];
        for (int i = 0; i < value.Length; i++)
        {
            bytes[i] = checked((byte)value[i]);
        }

        output.Write(bytes, 0, bytes.Length);
    }

    private readonly struct PageTreeEntry
    {
        internal PageTreeEntry(PdfObject.Ref reference, int pageCount)
        {
            Reference = reference;
            PageCount = pageCount;
        }

        internal PdfObject.Ref Reference { get; }

        internal int PageCount { get; }
    }

    private sealed class CountingWriteStream : Stream
    {
        private readonly Stream _inner;

        internal CountingWriteStream(Stream inner)
        {
            _inner = inner;
        }

        internal long Count { get; private set; }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => Count;

        public override long Position
        {
            get => Count;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
            Count = checked(Count + count);
        }

        public override void WriteByte(byte value)
        {
            _inner.WriteByte(value);
            Count = checked(Count + 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Flush();
            }

            base.Dispose(disposing);
        }
    }
}
