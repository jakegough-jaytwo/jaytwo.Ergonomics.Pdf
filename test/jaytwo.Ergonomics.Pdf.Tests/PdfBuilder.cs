#pragma warning disable SA1204
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace jaytwo.Ergonomics.Pdf.Tests;

internal sealed class PdfBuilder
{
    private readonly MemoryStream _body = new MemoryStream();
    private readonly List<Entry> _entries = new List<Entry>();

    public PdfBuilder(string preamble = "", string version = "1.7")
    {
        if (preamble.Length > 0)
        {
            Ascii(preamble);
        }

        Ascii("%PDF-" + version + "\n");
    }

    public long Length => _body.Length;

    public byte[] Snapshot()
    {
        return _body.ToArray();
    }

    public long OffsetOf(int id)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].Id == id && _entries[i].Type == 1)
            {
                return _entries[i].Field1;
            }
        }

        throw new InvalidOperationException("Object " + id.ToString(CultureInfo.InvariantCulture) + " has no file offset.");
    }

    public static PdfBuilder Basic(string catalogExtra = "", string pageExtra = "", string version = "1.7")
    {
        var builder = new PdfBuilder(version: version);
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R" + catalogExtra + " >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792]" + pageExtra + " >>");
        return builder;
    }

    public void Ascii(string text)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(text);
        _body.Write(bytes, 0, bytes.Length);
    }

    public void Bytes(byte[] data)
    {
        _body.Write(data, 0, data.Length);
    }

    public void Indirect(int id, string body)
    {
        long offset = _body.Length;
        Ascii(id.ToString(CultureInfo.InvariantCulture) + " 0 obj\n" + body + "\nendobj\n");
        _entries.Add(new Entry(id, 1, offset, 0));
    }

    public void StreamObject(int id, string dictionary, byte[] data)
    {
        long offset = _body.Length;
        Ascii(id.ToString(CultureInfo.InvariantCulture) + " 0 obj\n<< " + dictionary + " /Length " + data.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n");
        Bytes(data);
        Ascii("\nendstream\nendobj\n");
        _entries.Add(new Entry(id, 1, offset, 0));
    }

    public void Compressed(int id, int streamId, int index)
    {
        _entries.Add(new Entry(id, 2, streamId, index));
    }

    public void Hole(int id, long offset)
    {
        _entries.Add(new Entry(id, 1, offset, 0));
    }

    public byte[] Finish(string extraTrailer = "", bool crlfEntries = true, string? entryEnding = null)
    {
        int size = MaxId() + 1;
        string ending = entryEnding ?? (crlfEntries ? "\r\n" : "\n");
        AppendClassic(_body.Length, "/Size " + size.ToString(CultureInfo.InvariantCulture) + " /Root 1 0 R " + extraTrailer, ending);
        return _body.ToArray();
    }

    public (byte[] Head, byte[] Tail) Split(long tailOffset, string extraTrailer)
    {
        int size = MaxId() + 1;
        byte[] xref = ClassicBytes(tailOffset, "/Size " + size.ToString(CultureInfo.InvariantCulture) + " /Root 1 0 R " + extraTrailer, "\r\n");
        if (xref.Length > 65536)
        {
            throw new InvalidOperationException("Xref tail does not fit in 64 KiB.");
        }

        var tail = new byte[65536];
        for (int i = 0; i < tail.Length; i++)
        {
            tail[i] = (byte)'\n';
        }

        Buffer.BlockCopy(xref, 0, tail, 0, xref.Length);
        return (_body.ToArray(), tail);
    }

    public long AppendXrefStream(int xrefId, string extraTrailer, Func<byte[], byte[]>? protect = null)
    {
        long offset = _body.Length;
        int size = Math.Max(MaxId(), xrefId) + 1;
        var rows = new List<byte[]>();
        for (int id = 0; id < size; id++)
        {
            if (id == xrefId)
            {
                rows.Add(Row(1, offset, 0));
                continue;
            }

            Entry? entry = Latest(id);
            if (id == 0 || entry is not Entry found)
            {
                rows.Add(Row(0, 0, 65535));
            }
            else
            {
                rows.Add(Row(found.Type, found.Field1, found.Field2));
            }
        }

        byte[] compressed = Zlib(PredictUp(rows));
        if (protect != null)
        {
            compressed = protect(compressed);
        }

        string dict = "<< /Type /XRef /Size " + size.ToString(CultureInfo.InvariantCulture)
            + " /W [1 4 2] /Index [0 " + size.ToString(CultureInfo.InvariantCulture) + "] /Root 1 0 R "
            + extraTrailer
            + " /Filter /FlateDecode /DecodeParms << /Predictor 12 /Columns 7 /Colors 1 /BitsPerComponent 8 >> /Length "
            + compressed.Length.ToString(CultureInfo.InvariantCulture) + " >>";
        Ascii(xrefId.ToString(CultureInfo.InvariantCulture) + " 0 obj\n" + dict + "\nstream\n");
        Bytes(compressed);
        Ascii("\nendstream\nendobj\nstartxref\n" + offset.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
        _entries.Add(new Entry(xrefId, 1, offset, 0));
        return offset;
    }

    private void AppendClassic(long xrefOffset, string trailer, string entryEnding)
    {
        byte[] xref = ClassicBytes(xrefOffset, trailer, entryEnding);
        _body.Write(xref, 0, xref.Length);
    }

    private byte[] ClassicBytes(long xrefOffset, string trailer, string entryEnding)
    {
        int max = MaxId();
        var builder = new StringBuilder();
        builder.Append("xref\n0 ").Append(max + 1).Append('\n');
        for (int id = 0; id <= max; id++)
        {
            Entry? entry = Latest(id);
            if (id == 0 || entry is not Entry found || found.Type != 1)
            {
                builder.Append(EntryText(0, 65535, 'f', entryEnding));
            }
            else
            {
                builder.Append(EntryText(found.Field1, found.Field2, 'n', entryEnding));
            }
        }

        builder.Append("trailer\n<< ").Append(trailer).Append(" >>\nstartxref\n");
        builder.Append(xrefOffset.ToString(CultureInfo.InvariantCulture)).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string EntryText(long offset, int generation, char flag, string ending)
    {
        return offset.ToString("D10", CultureInfo.InvariantCulture)
            + " "
            + generation.ToString("D5", CultureInfo.InvariantCulture)
            + " "
            + flag
            + ending;
    }

    private int MaxId()
    {
        int max = 0;
        foreach (Entry entry in _entries)
        {
            if (entry.Id > max)
            {
                max = entry.Id;
            }
        }

        return max;
    }

    private Entry? Latest(int id)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].Id == id)
            {
                return _entries[i];
            }
        }

        return null;
    }

    private static byte[] Row(int type, long field1, int field2)
    {
        return new byte[]
        {
            (byte)type,
            (byte)(field1 >> 24),
            (byte)(field1 >> 16),
            (byte)(field1 >> 8),
            (byte)field1,
            (byte)(field2 >> 8),
            (byte)field2,
        };
    }

    private static byte[] PredictUp(List<byte[]> rows)
    {
        int width = rows[0].Length;
        var output = new byte[rows.Count * (width + 1)];
        var previous = new byte[width];
        for (int row = 0; row < rows.Count; row++)
        {
            int at = row * (width + 1);
            output[at] = 2;
            for (int i = 0; i < width; i++)
            {
                output[at + 1 + i] = (byte)(rows[row][i] - previous[i]);
                previous[i] = rows[row][i];
            }
        }

        return output;
    }

    private static byte[] Zlib(byte[] data)
    {
        return TestCompat.Zlib(data);
    }

    private readonly struct Entry
    {
        public Entry(int id, byte type, long field1, int field2)
        {
            Id = id;
            Type = type;
            Field1 = field1;
            Field2 = field2;
        }

        public int Id { get; }

        public byte Type { get; }

        public long Field1 { get; }

        public int Field2 { get; }
    }
}
