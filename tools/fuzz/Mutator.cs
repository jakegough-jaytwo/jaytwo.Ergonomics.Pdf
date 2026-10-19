using System;
using System.Collections.Generic;
using System.Text;

namespace jaytwo.Ergonomics.Pdf.Fuzz;

/// <summary>
/// Deterministic structure-aware mutations for the smoke run. A seeded <see cref="Random"/> keeps runs reproducible.
/// Campaigns use libFuzzer's own mutator with <c>pdf.dict</c> instead.
/// </summary>
internal sealed class Mutator
{
    private static readonly string[] Numbers =
    {
        "0", "-1", "1", "2", "255", "256", "65535", "65536", "2147483647", "2147483648", "-2147483648", "-2147483649",
        "4294967295", "4294967296", "9999999999", "9223372036854775807", "99999999999999999999", "1e9", "0.5", "-0",
    };

    private static readonly string[] Tokens =
    {
        "/Length", "/Filter", "/FlateDecode", "/LZWDecode", "/ASCII85Decode", "/ASCIIHexDecode", "/RunLengthDecode",
        "/DecodeParms", "/Predictor", "/Columns", "/Colors", "/BitsPerComponent", "/EarlyChange", "/Type", "/ObjStm",
        "/XRef", "/N", "/First", "/Extends", "/Size", "/W", "/Index", "/Prev", "/XRefStm", "/Root", "/Info", "/Encrypt",
        "/ID", "/Kids", "/Parent", "/Count", "/Fields", "/T", "/FT", "/V", "/AS", "/AP", "/Opt", "/Ff", "/DA", "/MK",
        "/Perms", "/DocMDP", "/Reference", "/TransformParams", "/P", "/R", "/O", "/U", "/OE", "/UE", "/CF", "/StmF",
        "/StrF", "/EmbeddedFiles", "/Names", "/EF", "/Metadata", "null", "true", "<<", ">>", "[", "]", "(", ")", "<", ">",
        " 0 R", " 1 R", "obj", "endobj", "stream\n", "\nendstream", "xref\n", "trailer", "startxref", "%%EOF", "\\", "%",
    };

    private readonly Random _random;

    public Mutator(int seed)
    {
        _random = new Random(seed);
    }

    public byte[] Mutate(byte[] input, IReadOnlyList<byte[]> pool, int maxLength)
    {
        var data = new List<byte>(input);
        int rounds = 1 + _random.Next(4);
        for (int i = 0; i < rounds; i++)
        {
            MutateOnce(data, pool);
        }

        if (data.Count > maxLength)
        {
            data.RemoveRange(maxLength, data.Count - maxLength);
        }

        return data.ToArray();
    }

    private static bool IsDigit(byte value)
    {
        return value >= (byte)'0' && value <= (byte)'9';
    }

    private void MutateOnce(List<byte> data, IReadOnlyList<byte[]> pool)
    {
        int at = data.Count == 0 ? 0 : _random.Next(data.Count);
        switch (_random.Next(9))
        {
            case 0 when data.Count > 0:
                data[at] ^= (byte)(1 << _random.Next(8));
                break;
            case 1 when data.Count > 0:
                data[at] = (byte)_random.Next(256);
                break;
            case 2:
                data.InsertRange(at, Encoding.ASCII.GetBytes(Tokens[_random.Next(Tokens.Length)]));
                break;
            case 3:
                ReplaceNumber(data, at);
                break;
            case 4 when data.Count > 0:
                data.RemoveRange(at, Math.Min(data.Count - at, 1 + _random.Next(64)));
                break;
            case 5 when data.Count > 0:
                int length = Math.Min(data.Count - at, 1 + _random.Next(256));
                data.InsertRange(_random.Next(data.Count), data.GetRange(at, length));
                break;
            case 6 when pool.Count > 0:
                byte[] other = pool[_random.Next(pool.Count)];
                if (other.Length > 0)
                {
                    int from = _random.Next(other.Length);
                    int count = Math.Min(other.Length - from, 1 + _random.Next(512));
                    data.InsertRange(at, new ArraySegment<byte>(other, from, count));
                }

                break;
            case 7 when data.Count > 0:
                data.RemoveRange(at, data.Count - at);
                break;
            default:
                data.Insert(at, (byte)_random.Next(256));
                break;
        }
    }

    /// <summary>
    /// Replaces the ASCII integer nearest to <paramref name="at"/> with a boundary value.
    /// </summary>
    private void ReplaceNumber(List<byte> data, int at)
    {
        byte[] number = Encoding.ASCII.GetBytes(Numbers[_random.Next(Numbers.Length)]);
        int start = at;
        while (start < data.Count && !IsDigit(data[start]))
        {
            start++;
        }

        if (start >= data.Count)
        {
            data.InsertRange(at, number);
            return;
        }

        int end = start;
        while (end < data.Count && IsDigit(data[end]))
        {
            end++;
        }

        data.RemoveRange(start, end - start);
        data.InsertRange(start, number);
    }
}
