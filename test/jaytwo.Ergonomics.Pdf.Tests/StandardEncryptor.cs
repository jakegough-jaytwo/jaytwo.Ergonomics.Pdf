#pragma warning disable SA1204
using System;
using System.Security.Cryptography;
using System.Text;

namespace jaytwo.Ergonomics.Pdf.Tests;

internal sealed class StandardEncryptor
{
    private static readonly byte[] Padding =
    {
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    };

    private readonly Kind _kind;
    private readonly int _keyBytes;
    private readonly byte[] _fileKey;
    private readonly byte[] _owner;
    private readonly byte[] _user;
    private readonly byte[] _ownerEncrypted;
    private readonly byte[] _userEncrypted;
    private readonly byte[] _perms;

    public StandardEncryptor(bool revision6, string userPassword, string ownerPassword, int permissions, bool encryptMetadata)
        : this(revision6 ? Kind.Revision6 : Kind.Revision4Aes, userPassword, ownerPassword, permissions, encryptMetadata, 128)
    {
    }

    public StandardEncryptor(Kind kind, string userPassword, string ownerPassword, int permissions, bool encryptMetadata, int keyBits = 128)
    {
        _kind = kind;
        _keyBytes = kind == Kind.Revision2 ? 5 : kind == Kind.Revision3 ? keyBits / 8 : kind == Kind.Revision5 || kind == Kind.Revision6 ? 32 : 16;
        if (kind == Kind.Revision3 && (keyBits < 40 || keyBits > 128 || (keyBits % 8) != 0))
        {
            throw new ArgumentOutOfRangeException(nameof(keyBits));
        }

        Permissions = permissions;
        EncryptMetadata = encryptMetadata;
        Id = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        if (kind == Kind.Revision5 || kind == Kind.Revision6)
        {
            _fileKey = new byte[32];
            for (int i = 0; i < _fileKey.Length; i++)
            {
                _fileKey[i] = (byte)(i + 1);
            }

            byte[] user = kind == Kind.Revision5 ? Utf8(userPassword) : Prepare(userPassword);
            byte[] owner = kind == Kind.Revision5 ? Utf8(ownerPassword) : Prepare(ownerPassword);
            var userValidation = new byte[] { 1, 1, 1, 1, 1, 1, 1, 1 };
            var userKeySalt = new byte[] { 2, 2, 2, 2, 2, 2, 2, 2 };
            var ownerValidation = new byte[] { 3, 3, 3, 3, 3, 3, 3, 3 };
            var ownerKeySalt = new byte[] { 4, 4, 4, 4, 4, 4, 4, 4 };
            _user = new byte[48];
            Buffer.BlockCopy(PasswordHash(kind, user, userValidation, null), 0, _user, 0, 32);
            Buffer.BlockCopy(userValidation, 0, _user, 32, 8);
            Buffer.BlockCopy(userKeySalt, 0, _user, 40, 8);
            _owner = new byte[48];
            Buffer.BlockCopy(PasswordHash(kind, owner, ownerValidation, _user), 0, _owner, 0, 32);
            Buffer.BlockCopy(ownerValidation, 0, _owner, 32, 8);
            Buffer.BlockCopy(ownerKeySalt, 0, _owner, 40, 8);
            _userEncrypted = AesCbc(PasswordHash(kind, user, userKeySalt, null), new byte[16], _fileKey, encrypt: true, padding: false);
            _ownerEncrypted = AesCbc(PasswordHash(kind, owner, ownerKeySalt, _user), new byte[16], _fileKey, encrypt: true, padding: false);
            _perms = AesEcb(_fileKey, PermissionBlock(permissions, encryptMetadata), encrypt: true);
        }
        else
        {
            byte[] paddedUser = Pad(Encoding.ASCII.GetBytes(userPassword));
            byte[] paddedOwner = Pad(Encoding.ASCII.GetBytes(ownerPassword));
            _owner = OwnerValue(paddedOwner, paddedUser);
            _fileKey = FileKey(paddedUser, _owner, permissions, Id, encryptMetadata);
            _user = kind == Kind.Revision2 ? Rc4(_fileKey, Padding) : new byte[32];
            if (kind != Kind.Revision2)
            {
                Buffer.BlockCopy(UserHash(_fileKey, Id), 0, _user, 0, 16);
            }

            _ownerEncrypted = Array.Empty<byte>();
            _userEncrypted = Array.Empty<byte>();
            _perms = Array.Empty<byte>();
        }
    }

    internal enum Kind
    {
        Revision2,
        Revision3,
        Revision4Aes,
        Revision4Rc4,
        Revision5,
        Revision6,
    }

    public byte[] Id { get; }

    public int Permissions { get; }

    public bool EncryptMetadata { get; }

    public static int FillOnly()
    {
        int value = (1 << 6) | (1 << 7) | (1 << 8);
        for (int bit = 13; bit <= 32; bit++)
        {
            value |= 1 << (bit - 1);
        }

        return value;
    }

    public string Dictionary(string? extra)
    {
        return Dictionary(extra, stringFilter: true, streamFilter: true);
    }

    // A false flag omits /StrF or /StmF, so a reader must apply the ISO 32000 default, Identity.
    public string Dictionary(string? extra, bool stringFilter, bool streamFilter)
    {
        var body = new StringBuilder();
        body.Append("<< /Filter /Standard ");
        if (_kind == Kind.Revision5 || _kind == Kind.Revision6)
        {
            body.Append("/V 5 /R ").Append(_kind == Kind.Revision5 ? "5" : "6");
            body.Append(" /Length 256 /CF << /StdCF << /CFM /AESV3 /Length 32 /AuthEvent /DocOpen >> >> ");
            body.Append("/OE ").Append(Hex(_ownerEncrypted)).Append(" /UE ").Append(Hex(_userEncrypted)).Append(" /Perms ").Append(Hex(_perms)).Append(' ');
        }
        else if (_kind == Kind.Revision2)
        {
            body.Append("/V 1 /R 2 ");
        }
        else if (_kind == Kind.Revision3)
        {
            body.Append("/V 2 /R 3 /Length ").Append((_keyBytes * 8).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(' ');
        }
        else if (_kind == Kind.Revision4Rc4)
        {
            body.Append("/V 4 /R 4 /Length 128 /CF << /StdCF << /CFM /V2 /Length 16 /AuthEvent /DocOpen >> >> ");
        }
        else
        {
            body.Append("/V 4 /R 4 /Length 128 /CF << /StdCF << /CFM /AESV2 /Length 16 /AuthEvent /DocOpen >> >> ");
        }

        body.Append("/O ").Append(Hex(_owner)).Append(" /U ").Append(Hex(_user));
        body.Append(" /P ").Append(Permissions.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (_kind == Kind.Revision4Aes || _kind == Kind.Revision4Rc4 || _kind == Kind.Revision5 || _kind == Kind.Revision6)
        {
            body.Append(streamFilter ? " /StmF /StdCF" : string.Empty).Append(stringFilter ? " /StrF /StdCF" : string.Empty);
        }

        if (!EncryptMetadata && (_kind == Kind.Revision4Aes || _kind == Kind.Revision4Rc4 || _kind == Kind.Revision5 || _kind == Kind.Revision6))
        {
            body.Append(" /EncryptMetadata false");
        }

        if (extra != null)
        {
            body.Append(' ').Append(extra);
        }

        body.Append(" >>");
        return body.ToString();
    }

    public string Text(string ascii, int objectNumber)
    {
        return Hex(Encrypt(Encoding.ASCII.GetBytes(ascii), objectNumber, 0));
    }

    public string IdLiteral()
    {
        string hex = Hex(Id);
        return hex + " " + hex;
    }

    public string Values()
    {
        return "/O " + Hex(_owner) + " /U " + Hex(_user) + " /P " + Permissions.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public byte[] Encrypt(byte[] plain, int objectNumber, int generation)
    {
        if (_kind == Kind.Revision2 || _kind == Kind.Revision3 || _kind == Kind.Revision4Rc4)
        {
            return Rc4(ObjectKey(objectNumber, generation), plain);
        }

        var iv = new byte[16];
        using var random = RandomNumberGenerator.Create();
        random.GetBytes(iv);
        return Encrypt(plain, objectNumber, generation, iv);
    }

    public byte[] Encrypt(byte[] plain, int objectNumber, int generation, byte[] iv)
    {
        byte[] body = AesCbc(ObjectKey(objectNumber, generation), iv, plain, encrypt: true, padding: true);
        var cipher = new byte[iv.Length + body.Length];
        Buffer.BlockCopy(iv, 0, cipher, 0, iv.Length);
        Buffer.BlockCopy(body, 0, cipher, iv.Length, body.Length);
        return cipher;
    }

    public byte[] EncryptRc4(byte[] plain, int objectNumber, int generation)
    {
        if (_kind != Kind.Revision4Aes && _kind != Kind.Revision4Rc4)
        {
            throw new InvalidOperationException("An RC4 object key requires a revision 4 file key.");
        }

        return Rc4(Rc4ObjectKey(objectNumber, generation), plain);
    }

    private byte[] ObjectKey(int objectNumber, int generation)
    {
        if (_kind == Kind.Revision5 || _kind == Kind.Revision6)
        {
            return _fileKey;
        }

        bool aes = _kind == Kind.Revision4Aes;
        var extra = new byte[aes ? 9 : 5];
        extra[0] = (byte)objectNumber;
        extra[1] = (byte)(objectNumber >> 8);
        extra[2] = (byte)(objectNumber >> 16);
        extra[3] = (byte)generation;
        extra[4] = (byte)(generation >> 8);
        if (aes)
        {
            extra[5] = (byte)'s';
            extra[6] = (byte)'A';
            extra[7] = (byte)'l';
            extra[8] = (byte)'T';
        }

        byte[] hash = Md5(_fileKey, extra);
        int length = aes ? 16 : Math.Min(_keyBytes + 5, 16);
        var key = new byte[length];
        Buffer.BlockCopy(hash, 0, key, 0, key.Length);
        return key;
    }

    private byte[] Rc4ObjectKey(int objectNumber, int generation)
    {
        var extra = new byte[5];
        extra[0] = (byte)objectNumber;
        extra[1] = (byte)(objectNumber >> 8);
        extra[2] = (byte)(objectNumber >> 16);
        extra[3] = (byte)generation;
        extra[4] = (byte)(generation >> 8);
        byte[] hash = Md5(_fileKey, extra);
        int length = Math.Min(_fileKey.Length + 5, 16);
        var key = new byte[length];
        Buffer.BlockCopy(hash, 0, key, 0, key.Length);
        return key;
    }

    private byte[] FileKey(byte[] paddedPassword, byte[] owner, int permissions, byte[] id, bool metadata)
    {
        using IncrementalHash md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(paddedPassword);
        md5.AppendData(owner, 0, 32);
        md5.AppendData(new[]
        {
            (byte)permissions,
            (byte)(permissions >> 8),
            (byte)(permissions >> 16),
            (byte)(permissions >> 24),
        });
        md5.AppendData(id);
        if ((_kind == Kind.Revision4Aes || _kind == Kind.Revision4Rc4) && !metadata)
        {
            md5.AppendData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
        }

        byte[] hash = md5.GetHashAndReset();
        if (_kind != Kind.Revision2)
        {
            for (int i = 0; i < 50; i++)
            {
                hash = Md5(hash, 0, _keyBytes);
            }
        }

        var key = new byte[_keyBytes];
        Buffer.BlockCopy(hash, 0, key, 0, key.Length);
        return key;
    }

    private static byte[] UserHash(byte[] fileKey, byte[] id)
    {
        byte[] result = Rc4(fileKey, Md5(Padding, id));
        for (int round = 1; round <= 19; round++)
        {
            result = Rc4(Xor(fileKey, round), result);
        }

        return result;
    }

    private byte[] OwnerValue(byte[] paddedOwner, byte[] paddedUser)
    {
        byte[] hash = Md5(paddedOwner);
        if (_kind != Kind.Revision2)
        {
            for (int i = 0; i < 50; i++)
            {
                hash = Md5(hash);
            }
        }

        var key = new byte[_keyBytes];
        Buffer.BlockCopy(hash, 0, key, 0, key.Length);
        byte[] data = Rc4(key, paddedUser);
        if (_kind == Kind.Revision2)
        {
            return data;
        }

        for (int round = 1; round <= 19; round++)
        {
            data = Rc4(Xor(key, round), data);
        }

        return data;
    }

    private static byte[] Pad(byte[] password)
    {
        var padded = new byte[32];
        int count = Math.Min(password.Length, 32);
        Buffer.BlockCopy(password, 0, padded, 0, count);
        if (count < 32)
        {
            Buffer.BlockCopy(Padding, 0, padded, count, 32 - count);
        }

        return padded;
    }

    private static byte[] PasswordHash(Kind kind, byte[] password, byte[] salt, byte[]? userKey)
    {
        if (kind == Kind.Revision5)
        {
            return userKey == null ? Sha(HashAlgorithmName.SHA256, password, salt) : Sha(HashAlgorithmName.SHA256, password, salt, userKey);
        }

        return Hash(password, salt, userKey);
    }

    private static byte[] Prepare(string password)
    {
        return Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormKC));
    }

    private static byte[] Utf8(string password)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(password);
        if (utf8.Length <= 127)
        {
            return utf8;
        }

        int length = 127;
        while (length > 0 && (utf8[length] & 0xC0) == 0x80)
        {
            length--;
        }

        var truncated = new byte[length];
        Buffer.BlockCopy(utf8, 0, truncated, 0, length);
        return truncated;
    }

    private static byte[] Hash(byte[] password, byte[] salt, byte[]? userKey)
    {
        byte[] user = userKey ?? Array.Empty<byte>();
        byte[] k = Sha(HashAlgorithmName.SHA256, password, salt, user);
        byte[] encrypted = Array.Empty<byte>();
        int round = 0;
        while (round < 64 || (encrypted[encrypted.Length - 1] & 0xFF) > round - 32)
        {
            byte[] block = Concat(password, k, user);
            var repeated = new byte[block.Length * 64];
            for (int i = 0; i < 64; i++)
            {
                Buffer.BlockCopy(block, 0, repeated, i * block.Length, block.Length);
            }

            var key = new byte[16];
            var iv = new byte[16];
            Buffer.BlockCopy(k, 0, key, 0, 16);
            Buffer.BlockCopy(k, 16, iv, 0, 16);
            encrypted = AesCbc(key, iv, repeated, encrypt: true, padding: false);
            int selector = 0;
            for (int i = 0; i < 16; i++)
            {
                selector += encrypted[i];
            }

            HashAlgorithmName name = (selector % 3) switch
            {
                0 => HashAlgorithmName.SHA256,
                1 => HashAlgorithmName.SHA384,
                _ => HashAlgorithmName.SHA512,
            };
            k = Sha(name, encrypted);
            round++;
        }

        var result = new byte[32];
        Buffer.BlockCopy(k, 0, result, 0, 32);
        return result;
    }

    private static byte[] PermissionBlock(int permissions, bool metadata)
    {
        var block = new byte[16];
        block[0] = (byte)permissions;
        block[1] = (byte)(permissions >> 8);
        block[2] = (byte)(permissions >> 16);
        block[3] = (byte)(permissions >> 24);
        block[4] = 0xFF;
        block[5] = 0xFF;
        block[6] = 0xFF;
        block[7] = 0xFF;
        block[8] = metadata ? (byte)'T' : (byte)'F';
        block[9] = (byte)'a';
        block[10] = (byte)'d';
        block[11] = (byte)'b';
        return block;
    }

    private static string Hex(byte[] data)
    {
        var chars = new char[(data.Length * 2) + 2];
        chars[0] = '<';
        chars[chars.Length - 1] = '>';
        for (int i = 0; i < data.Length; i++)
        {
            chars[(i * 2) + 1] = Nibble(data[i] >> 4);
            chars[(i * 2) + 2] = Nibble(data[i] & 0xF);
        }

        return new string(chars);
    }

    private static char Nibble(int value)
    {
        return (char)(value < 10 ? '0' + value : 'A' + (value - 10));
    }

    private static byte[] Md5(params byte[][] parts)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        foreach (byte[] part in parts)
        {
            hash.AppendData(part);
        }

        return hash.GetHashAndReset();
    }

    private static byte[] Md5(byte[] data, int offset, int count)
    {
        var slice = new byte[count];
        Buffer.BlockCopy(data, offset, slice, 0, count);
        return Md5(slice);
    }

    private static byte[] Sha(HashAlgorithmName name, params byte[][] parts)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(name);
        foreach (byte[] part in parts)
        {
            if (part.Length > 0)
            {
                hash.AppendData(part);
            }
        }

        return hash.GetHashAndReset();
    }

    private static byte[] Concat(params byte[][] parts)
    {
        int length = 0;
        foreach (byte[] part in parts)
        {
            length += part.Length;
        }

        var output = new byte[length];
        int offset = 0;
        foreach (byte[] part in parts)
        {
            Buffer.BlockCopy(part, 0, output, offset, part.Length);
            offset += part.Length;
        }

        return output;
    }

    private static byte[] AesCbc(byte[] key, byte[] iv, byte[] data, bool encrypt, bool padding)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = padding ? PaddingMode.PKCS7 : PaddingMode.None;
        aes.Key = key;
        aes.IV = iv;
        using ICryptoTransform transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
        return transform.TransformFinalBlock(data, 0, data.Length);
    }

    private static byte[] AesEcb(byte[] key, byte[] data, bool encrypt)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = key;
        using ICryptoTransform transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor(key, new byte[16]);
        return transform.TransformFinalBlock(data, 0, data.Length);
    }

    private static byte[] Rc4(byte[] key, byte[] data)
    {
        var state = new byte[256];
        for (int i = 0; i < state.Length; i++)
        {
            state[i] = (byte)i;
        }

        int j = 0;
        for (int i = 0; i < state.Length; i++)
        {
            j = (j + state[i] + key[i % key.Length]) & 255;
            byte swap = state[i];
            state[i] = state[j];
            state[j] = swap;
        }

        var output = new byte[data.Length];
        int a = 0;
        int b = 0;
        for (int i = 0; i < data.Length; i++)
        {
            a = (a + 1) & 255;
            b = (b + state[a]) & 255;
            byte swap = state[a];
            state[a] = state[b];
            state[b] = swap;
            output[i] = (byte)(data[i] ^ state[(state[a] + state[b]) & 255]);
        }

        return output;
    }

    private static byte[] Xor(byte[] key, int round)
    {
        var mixed = new byte[key.Length];
        for (int i = 0; i < key.Length; i++)
        {
            mixed[i] = (byte)(key[i] ^ round);
        }

        return mixed;
    }
}
