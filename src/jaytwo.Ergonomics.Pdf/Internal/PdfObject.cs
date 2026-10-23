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
        private readonly List<(string Key, PdfObject Value)> _items;

        public Dict()
        {
            _items = new List<(string, PdfObject)>();
        }

        // W-115: the parser and DeepClone know the entry count, so the list is made at its final size.
        public Dict(int capacity)
        {
            _items = new List<(string, PdfObject)>(capacity);
        }

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

        // The parser calls these once it tracks key positions itself, so a large dictionary is not scanned per key.
        internal void Append(string key, PdfObject value)
        {
            _items.Add((key, value));
        }

        internal void ReplaceAt(int index, PdfObject value)
        {
            _items[index] = (_items[index].Key, value);
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
            var clone = new Dict(_items.Count);
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

        public Arr(int capacity)
        {
            Items = new List<PdfObject>(capacity);
        }

        public List<PdfObject> Items { get; }

        public override PdfObject DeepClone()
        {
            var clone = new Arr(Items.Count);
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

    // Caller content for an embedded file. Read measures it once, for /Params /CheckSum and, when compressed, the
    // encoded /Length the serializer writes before the body. CopyTo reads it again and refuses bytes that no longer
    // match, so a save never publishes a checksum or length the content does not have.
    internal sealed class ExternalBody : StreamBody
    {
        private const string ChangedMessage = "Attachment content changed after it was added.";

        private readonly Stream _stream;
        private readonly long _start;
        private readonly long _contentLength;
        private readonly bool _compress;
        private readonly byte[] _checksum;
        private readonly long _length;

        private ExternalBody(Stream stream, long start, long contentLength, bool compress, byte[] checksum, long length)
        {
            _stream = stream;
            _start = start;
            _contentLength = contentLength;
            _compress = compress;
            _checksum = checksum;
            _length = length;
        }

        public Stream Source => _stream;

        public long Start => _start;

        // The content's own bytes, before compression.
        public long ContentLength => _contentLength;

        // The MD5 of the content.
        public byte[] Checksum => (byte[])_checksum.Clone();

        // The bytes written between stream and endstream, before encryption.
        public override long Length => _length;

        public static ExternalBody Read(Stream stream, long start, long contentLength, bool compress)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            (byte[] checksum, long length) = PdfFlateEncoder.Copy(stream, start, contentLength, null, compress);
            return new ExternalBody(stream, start, contentLength, compress, checksum, length);
        }

        public override void CopyTo(Stream destination)
        {
            byte[] checksum;
            long written;
            try
            {
                (checksum, written) = PdfFlateEncoder.Copy(_stream, _start, _contentLength, destination, _compress);
            }
            catch (EndOfStreamException exception)
            {
                throw new InvalidOperationException(ChangedMessage, exception);
            }

            if (written != _length || !checksum.AsSpan().SequenceEqual(_checksum))
            {
                throw new InvalidOperationException(ChangedMessage);
            }
        }
    }
}
