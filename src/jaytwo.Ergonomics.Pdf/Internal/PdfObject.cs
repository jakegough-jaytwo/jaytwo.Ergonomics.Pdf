#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace jaytwo.Ergonomics.Pdf.Internal;

internal abstract class PdfObject
{
    public int ObjectNumber { get; set; }

    public int Generation { get; set; }

    public abstract PdfObject DeepClone();

    internal sealed class Dict : PdfObject
    {
        private readonly List<(string Key, PdfObject Value)> _items = new List<(string, PdfObject)>();

        public StreamBody? Body { get; set; }

        public IReadOnlyList<(string Key, PdfObject Value)> Items => _items;

        public PdfObject? Get(string key)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].Key == key)
                {
                    return _items[i].Value;
                }
            }

            return null;
        }

        public bool Contains(string key)
        {
            return Get(key) != null;
        }

        public void Set(string key, PdfObject value)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].Key == key)
                {
                    _items[i] = (key, value);
                    return;
                }
            }

            _items.Add((key, value));
        }

        public void Remove(string key)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].Key == key)
                {
                    _items.RemoveAt(i);
                }
            }
        }

        public override PdfObject DeepClone()
        {
            var clone = new Dict();
            foreach (var item in _items)
            {
                clone._items.Add((item.Key, item.Value.DeepClone()));
            }

            clone.Body = Body;
            clone.ObjectNumber = ObjectNumber;
            clone.Generation = Generation;
            return clone;
        }
    }

    internal sealed class Arr : PdfObject
    {
        public Arr()
        {
            Items = new List<PdfObject>();
        }

        public List<PdfObject> Items { get; }

        public override PdfObject DeepClone()
        {
            var clone = new Arr();
            foreach (var item in Items)
            {
                clone.Items.Add(item.DeepClone());
            }

            clone.ObjectNumber = ObjectNumber;
            clone.Generation = Generation;
            return clone;
        }
    }

    internal sealed class Num : PdfObject
    {
        private Num(bool isInteger, long longValue, double doubleValue)
        {
            IsInteger = isInteger;
            LongValue = longValue;
            DoubleValue = doubleValue;
        }

        public bool IsInteger { get; }

        public long LongValue { get; }

        public double DoubleValue { get; }

        public static Num FromLong(long value)
        {
            return new Num(true, value, value);
        }

        public static Num FromDouble(double value)
        {
            return new Num(false, 0, value);
        }

        public override PdfObject DeepClone()
        {
            var clone = new Num(IsInteger, LongValue, DoubleValue);
            clone.ObjectNumber = ObjectNumber;
            clone.Generation = Generation;
            return clone;
        }

        public string Format()
        {
            if (IsInteger)
            {
                return LongValue.ToString(CultureInfo.InvariantCulture);
            }

            if (double.IsNaN(DoubleValue) || double.IsInfinity(DoubleValue))
            {
                return "0";
            }

            var text = DoubleValue.ToString("0.########", CultureInfo.InvariantCulture);
            return text;
        }
    }

    internal sealed class Name : PdfObject
    {
        public Name(string value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string Value { get; }

        public override PdfObject DeepClone()
        {
            var clone = new Name(Value);
            clone.ObjectNumber = ObjectNumber;
            clone.Generation = Generation;
            return clone;
        }
    }

    internal sealed class Str : PdfObject
    {
        public Str(byte[] raw, bool preferHex)
        {
            Raw = raw ?? throw new ArgumentNullException(nameof(raw));
            PreferHex = preferHex;
        }

        public byte[] Raw { get; private set; }

        public bool PreferHex { get; }

        internal void Replace(byte[] raw)
        {
            Raw = raw ?? throw new ArgumentNullException(nameof(raw));
        }

        public override PdfObject DeepClone()
        {
            var copy = new byte[Raw.Length];
            Buffer.BlockCopy(Raw, 0, copy, 0, Raw.Length);
            var clone = new Str(copy, PreferHex);
            clone.ObjectNumber = ObjectNumber;
            clone.Generation = Generation;
            return clone;
        }
    }

    internal sealed class Ref : PdfObject
    {
        public Ref(int objectId, int generation)
        {
            ObjectId = objectId;
            GenerationNumber = generation;
        }

        public int ObjectId { get; }

        public int GenerationNumber { get; }

        public override PdfObject DeepClone()
        {
            return new Ref(ObjectId, GenerationNumber);
        }
    }

    internal sealed class Bool : PdfObject
    {
        private Bool(bool value)
        {
            Value = value;
        }

        public bool Value { get; }

        public static Bool True { get; } = new Bool(true);

        public static Bool False { get; } = new Bool(false);

        public static Bool From(bool value)
        {
            return value ? True : False;
        }

        public override PdfObject DeepClone()
        {
            return this;
        }
    }

    internal sealed class NullObj : PdfObject
    {
        private NullObj()
        {
        }

        public static NullObj Instance { get; } = new NullObj();

        public override PdfObject DeepClone()
        {
            return this;
        }
    }

    internal sealed class ByteRangeSlot : PdfObject
    {
        public long DigitPosition { get; set; } = -1;

        public override PdfObject DeepClone()
        {
            return new ByteRangeSlot();
        }
    }

    internal sealed class ContentsSlot : PdfObject
    {
        public ContentsSlot(int reservedBytes)
        {
            ReservedBytes = reservedBytes;
        }

        public int ReservedBytes { get; }

        public long LessThanPosition { get; set; } = -1;

        public long HexPosition { get; set; } = -1;

        public override PdfObject DeepClone()
        {
            return new ContentsSlot(ReservedBytes);
        }
    }

    internal abstract class StreamBody
    {
        public abstract long Length { get; }

        public abstract void CopyTo(Stream destination);
    }

    internal sealed class MemoryBody : StreamBody
    {
        private readonly byte[] _data;

        public MemoryBody(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public byte[] Data => _data;

        public override long Length => _data.Length;

        public override void CopyTo(Stream destination)
        {
            destination.Write(_data, 0, _data.Length);
        }
    }

    internal sealed class FileBody : StreamBody
    {
        public FileBody(long offset, long length)
        {
            Offset = offset;
            Length = length;
        }

        public long Offset { get; }

        public override long Length { get; }

        public override void CopyTo(Stream destination)
        {
            throw new InvalidOperationException("File-backed streams are copied by the writer.");
        }
    }

    internal sealed class ExternalBody : StreamBody
    {
        private readonly Stream _stream;
        private readonly long _start;
        private readonly long _length;

        public ExternalBody(Stream stream, long start, long length)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _start = start;
            _length = length;
        }

        public Stream Source => _stream;

        public long Start => _start;

        public override long Length => _length;

        public override void CopyTo(Stream destination)
        {
            _stream.Seek(_start, SeekOrigin.Begin);
            var buffer = new byte[65536];
            long remaining = _length;
            while (remaining > 0)
            {
                int count = (int)Math.Min(buffer.Length, remaining);
                int read = _stream.Read(buffer, 0, count);
                if (read == 0)
                {
                    throw new EndOfStreamException("Attachment stream ended before its declared length.");
                }

                destination.Write(buffer, 0, read);
                remaining -= read;
            }
        }
    }
}
