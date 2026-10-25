using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using jaytwo.Pdf.Internal;
using Xunit;

namespace jaytwo.Pdf.Tests;

public sealed class NewFileWriterTests
{
    [Fact]
    public void new_file_has_effective_header_classic_xref_and_a_64_way_page_tree()
    {
        byte[] pdf = BlankDocument(65, 17);

        Assert.StartsWith("%PDF-1.7", Encoding.ASCII.GetString(pdf));
        Assert.Contains("\nxref\r\n", Encoding.ASCII.GetString(pdf));
        Assert.DoesNotContain("/ObjStm", Encoding.ASCII.GetString(pdf));
        using var stream = new MemoryStream(pdf, false);
        using PdfDocument document = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal(65, document.PageCount);
    }

    [Fact]
    public void header_is_never_earlier_than_1_5_and_output_need_not_seek()
    {
        using var bytes = new MemoryStream();
        using var destination = new NonSeekableWriteStream(bytes);
        WriteBlankDocument(destination, 1, 14);

        Assert.StartsWith("%PDF-1.5", Encoding.ASCII.GetString(bytes.ToArray()));
    }

    [Theory]
    [InlineData(9999999999L, true)]
    [InlineData(10000000000L, false)]
    public void xref_format_changes_at_the_ten_digit_offset_boundary(long offset, bool classic)
    {
        Assert.Equal(classic, PdfNewFileWriter.UsesClassicXref(offset));
    }

    [Fact]
    public void copier_registers_before_children_and_qualifies_the_map_by_source()
    {
        using PdfObjectStore first = StoreWithCycle();
        using PdfObjectStore second = StoreWithCycle();
        var budget = new PdfNewFileBudget(new PdfNewFileLimits());
        var writer = new PdfNewFileWriter(budget);
        var copier = new PdfTypedObjectCopier(writer, budget);

        PdfObject.Ref firstCopy = copier.Copy(first, new PdfObject.Ref(1, 0), "cycle");
        PdfObject.Ref sameCopy = copier.Copy(first, new PdfObject.Ref(1, 0), "cycle");
        PdfObject.Ref secondCopy = copier.Copy(second, new PdfObject.Ref(1, 0), "cycle");

        Assert.Equal(firstCopy.ObjectId, sameCopy.ObjectId);
        Assert.NotEqual(firstCopy.ObjectId, secondCopy.ObjectId);
    }

    [Fact]
    public void copier_applies_typed_omit_and_remap_rules()
    {
        using var source = new PdfObjectStore(new TestPdfSource(Array.Empty<byte>()), ownsSource: true, cursorWindows: 1);
        var dictionary = new PdfObject.Dict();
        dictionary.Set("Keep", PdfObject.Num.FromLong(1));
        dictionary.Set("Drop", PdfObject.Num.FromLong(2));
        dictionary.Set("Change", PdfObject.Num.FromLong(3));
        Add(source, 1, dictionary);
        var budget = new PdfNewFileBudget(new PdfNewFileLimits());
        var writer = new PdfNewFileWriter(budget);
        var copier = new PdfTypedObjectCopier(writer, budget, context => context.Key switch
        {
            "Drop" => PdfTypedObjectCopier.CopyDecision.Omit(),
            "Change" => PdfTypedObjectCopier.CopyDecision.Remap(PdfObject.Num.FromLong(4)),
            _ => PdfTypedObjectCopier.CopyDecision.Copy(),
        });

        PdfObject.Ref copied = copier.Copy(source, new PdfObject.Ref(1, 0), "test");
        var catalog = new PdfObject.Dict();
        catalog.Set("Type", new PdfObject.Name("Catalog"));
        catalog.Set("Value", copied);
        PdfObject.Ref root = writer.Register(catalog);
        using var output = new MemoryStream();
        writer.Write(output, root, null, Identifier(), 15);
        string text = Encoding.ASCII.GetString(output.ToArray());

        Assert.Contains("/Keep 1", text);
        Assert.DoesNotContain("/Drop", text);
        Assert.Contains("/Change 4", text);
    }

    [Fact]
    public void raw_image_stream_bytes_are_unchanged_and_length_follows_the_stream()
    {
        byte[] encoded = { 0x78, 0x9c, 0x63, 0x60, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01 };
        using var source = new PdfObjectStore(new TestPdfSource(encoded), ownsSource: true, cursorWindows: 1);
        var image = new PdfObject.Dict();
        image.Set("Type", new PdfObject.Name("XObject"));
        image.Set("Subtype", new PdfObject.Name("Image"));
        image.Set("Filter", new PdfObject.Name("FlateDecode"));
        image.Body = new PdfObject.FileBody(0, encoded.Length);
        Add(source, 1, image);
        byte[] output = CopyOneObject(source);

        Assert.True(IndexOf(output, encoded) >= 0, BitConverter.ToString(output));
        string text = Encoding.ASCII.GetString(output);
        int lengthReference = text.IndexOf("/Length ", StringComparison.Ordinal);
        int endStream = text.IndexOf("\nendstream\nendobj\n", StringComparison.Ordinal);
        Assert.True(lengthReference >= 0);
        Assert.True(endStream > lengthReference);
        string after = text.Substring(endStream + "\nendstream\nendobj\n".Length);
        Assert.Contains(" obj\n10\nendobj", after);
    }

    [Fact]
    [Trait(Lane.Trait, Lane.LargeData)]
    public void sparse_stream_is_copied_with_a_bounded_buffer()
    {
        const long length = 8L * 1024 * 1024;
        var sparse = new SparseSource(length);
        using var source = new PdfObjectStore(sparse, ownsSource: true, cursorWindows: 1);
        var stream = new PdfObject.Dict();
        stream.Body = new PdfObject.FileBody(0, length);
        Add(source, 1, stream);
        var budget = new PdfNewFileBudget(new PdfNewFileLimits());
        var writer = new PdfNewFileWriter(budget);
        var copier = new PdfTypedObjectCopier(writer, budget);
        PdfObject.Ref root = copier.Copy(source, new PdfObject.Ref(1, 0), "stream");
        using var output = new CountingSink();

        writer.Write(output, root, null, Identifier(), 15);

        Assert.True(output.Length > length);
        Assert.InRange(sparse.LargestRead, 1, 65536);
    }

    [Fact]
    public void every_new_file_budget_raises_the_dedicated_error()
    {
        Assert.Equal("structural memory", Assert.Throws<PdfResourceLimitException>(() =>
        {
            var budget = new PdfNewFileBudget(new PdfNewFileLimits(structuralBytes: 1));
            new PdfNewFileWriter(budget).Register(new PdfObject.Dict());
        }).Resource);

        Assert.Equal("output pages", Assert.Throws<PdfResourceLimitException>(() =>
        {
            var budget = new PdfNewFileBudget(new PdfNewFileLimits(outputPages: 0));
            var writer = new PdfNewFileWriter(budget);
            writer.BuildPageTree(new[] { writer.Register(Page()) });
        }).Resource);

        using PdfObjectStore chain = StoreWithChain();
        Assert.Equal("mapped objects", Assert.Throws<PdfResourceLimitException>(() =>
        {
            var budget = new PdfNewFileBudget(new PdfNewFileLimits(mappedObjects: 1));
            var copier = new PdfTypedObjectCopier(new PdfNewFileWriter(budget), budget);
            copier.Copy(chain, new PdfObject.Ref(1, 0), "chain");
        }).Resource);

        Assert.Equal("traversal depth", Assert.Throws<PdfResourceLimitException>(() =>
        {
            var budget = new PdfNewFileBudget(new PdfNewFileLimits(traversalDepth: 1));
            var copier = new PdfTypedObjectCopier(new PdfNewFileWriter(budget), budget);
            copier.Copy(chain, new PdfObject.Ref(1, 0), "chain");
        }).Resource);
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void qpdf_and_poppler_open_the_new_file()
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "new-file.pdf");
            File.WriteAllBytes(path, BlankDocument(65, 17));
            string qpdf = ExternalTool.Locate("JAYTWO_QPDF", ExternalTool.Executable("qpdf"));
            string pdfinfo = ExternalTool.Locate("JAYTWO_PDFINFO", ExternalTool.Executable("pdfinfo"));
            ExternalTool.Output(ExternalTool.Start(qpdf, "--check", path));
            string info = ExternalTool.Output(ExternalTool.Start(pdfinfo, path));
            Assert.Contains("Pages:", info);
            Assert.Contains("65", info);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static byte[] BlankDocument(int pageCount, int version)
    {
        using var output = new MemoryStream();
        WriteBlankDocument(output, pageCount, version);
        return output.ToArray();
    }

    private static void WriteBlankDocument(Stream output, int pageCount, int version)
    {
        var budget = new PdfNewFileBudget(new PdfNewFileLimits());
        var writer = new PdfNewFileWriter(budget);
        var pages = new List<PdfObject.Ref>(pageCount);
        for (int i = 0; i < pageCount; i++)
        {
            pages.Add(writer.Register(Page()));
        }

        PdfObject.Ref pagesRoot = writer.BuildPageTree(pages);
        var catalog = new PdfObject.Dict();
        catalog.Set("Type", new PdfObject.Name("Catalog"));
        catalog.Set("Pages", pagesRoot);
        PdfObject.Ref root = writer.Register(catalog);
        var info = new PdfObject.Dict();
        info.Set("Producer", new PdfObject.Str(Encoding.ASCII.GetBytes("jaytwo.Pdf"), false));
        PdfObject.Ref infoReference = writer.Register(info);
        writer.Write(output, root, infoReference, Identifier(), version);
    }

    private static PdfObject.Dict Page()
    {
        var box = new PdfObject.Arr(4);
        box.Items.Add(PdfObject.Num.FromLong(0));
        box.Items.Add(PdfObject.Num.FromLong(0));
        box.Items.Add(PdfObject.Num.FromLong(72));
        box.Items.Add(PdfObject.Num.FromLong(72));
        var page = new PdfObject.Dict();
        page.Set("Type", new PdfObject.Name("Page"));
        page.Set("MediaBox", box);
        page.Set("Resources", new PdfObject.Dict());
        return page;
    }

    private static PdfObject.Arr Identifier()
    {
        var identifier = new PdfObject.Arr(2);
        identifier.Items.Add(new PdfObject.Str(new byte[16], true));
        identifier.Items.Add(new PdfObject.Str(new byte[16], true));
        return identifier;
    }

    private static PdfObjectStore StoreWithCycle()
    {
        var store = new PdfObjectStore(new TestPdfSource(Array.Empty<byte>()), ownsSource: true, cursorWindows: 1);
        var first = new PdfObject.Dict();
        first.Set("Next", new PdfObject.Ref(2, 0));
        var second = new PdfObject.Dict();
        second.Set("Next", new PdfObject.Ref(1, 0));
        Add(store, 1, first);
        Add(store, 2, second);
        return store;
    }

    private static PdfObjectStore StoreWithChain()
    {
        var store = new PdfObjectStore(new TestPdfSource(Array.Empty<byte>()), ownsSource: true, cursorWindows: 1);
        var first = new PdfObject.Dict();
        first.Set("Next", new PdfObject.Ref(2, 0));
        Add(store, 1, first);
        Add(store, 2, new PdfObject.Dict());
        return store;
    }

    private static void Add(PdfObjectStore store, int id, PdfObject value)
    {
        value.ObjectNumber = id;
        value.Generation = 0;
        store.Cache.Add(id, value);
        store.Xref.Add(id, new XrefEntry(1, 0, 0));
        store.Size = Math.Max(store.Size, id + 1);
    }

    private static byte[] CopyOneObject(PdfObjectStore source)
    {
        var budget = new PdfNewFileBudget(new PdfNewFileLimits());
        var writer = new PdfNewFileWriter(budget);
        var copier = new PdfTypedObjectCopier(writer, budget);
        PdfObject.Ref copied = copier.Copy(source, new PdfObject.Ref(1, 0), "image");
        var catalog = new PdfObject.Dict();
        catalog.Set("Type", new PdfObject.Name("Catalog"));
        catalog.Set("Image", copied);
        PdfObject.Ref root = writer.Register(catalog);
        using var output = new MemoryStream();
        writer.Write(output, root, null, Identifier(), 15);
        return output.ToArray();
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int start = 0; start <= haystack.Length - needle.Length; start++)
        {
            int index = 0;
            while (index < needle.Length && haystack[start + index] == needle[index])
            {
                index++;
            }

            if (index == needle.Length)
            {
                return start;
            }
        }

        return -1;
    }

    private sealed class NonSeekableWriteStream : Stream
    {
        private readonly Stream _inner;

        internal NonSeekableWriteStream(Stream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        public override void WriteByte(byte value) => _inner.WriteByte(value);
    }

    private sealed class CountingSink : Stream
    {
        private long _length;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _length;

        public override long Position
        {
            get => Length;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => _length = checked(_length + count);

        public override void WriteByte(byte value) => _length = checked(_length + 1);
    }

    private sealed class SparseSource : IPdfSource
    {
        internal SparseSource(long length)
        {
            Length = length;
        }

        public long Length { get; }

        internal int LargestRead { get; private set; }

        public int Read(long position, Span<byte> buffer)
        {
            if (position >= Length)
            {
                return 0;
            }

            int count = (int)Math.Min(buffer.Length, Length - position);
            LargestRead = Math.Max(LargestRead, count);
            for (int i = 0; i < count; i++)
            {
                buffer[i] = (byte)((position + i) % 251);
            }

            return count;
        }

        public ValueTask<int> ReadAsync(long position, Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new ValueTask<int>(Read(position, buffer.Span));
        }

        public void Dispose()
        {
        }
    }
}
