#pragma warning disable SA1201, SA1202, SA1204
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace jaytwo.Pdf.Internal;

internal sealed class PdfSecurity
{
    private const int StringChunk = 4096;

    private static readonly byte[] PasswordPadding =
    {
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    };

    private enum CipherKind
    {
        None,
        Rc4,
        Aes128,
        Aes256,
    }

    private readonly byte[] _fileKey;
    private readonly int _encryptId;
    private readonly CipherKind _strings;
    private readonly CipherKind _streams;
    private readonly CipherKind _embeddedFiles;
    private readonly bool _metadata;
    private readonly bool _owner;
    private readonly PdfPermissions _userPermissions;

    private PdfSecurity(
        byte[] fileKey,
        int encryptId,
        CipherKind strings,
        CipherKind streams,
        CipherKind embeddedFiles,
        bool metadata,
        bool owner,
        int revision,
        int permissions)
    {
        _fileKey = fileKey;
        _encryptId = encryptId;
        _strings = strings;
        _streams = streams;
        _embeddedFiles = embeddedFiles;
        _metadata = metadata;
        _owner = owner;
        _userPermissions = PdfPermissions.FromStandard(revision, permissions);
    }

    public long CipherLength(PdfObject.Dict dict, long plainLength)
    {
        if (plainLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(plainLength));
        }

        CipherKind kind = CipherFor(dict);
        if (kind == CipherKind.Rc4 || kind == CipherKind.None)
        {
            return plainLength;
        }

        return checked((((plainLength / 16) + 1) * 16) + 16);
    }

    public static bool IsSupportedRevision(PdfObject.Dict encrypt)
    {
        if (ReadName(encrypt, "Filter") != "Standard" || encrypt.Contains("SubFilter"))
        {
            return false;
        }

        if (encrypt.Get("V") is not PdfObject.Num version || encrypt.Get("R") is not PdfObject.Num revision)
        {
            return false;
        }

        long versionValue = version.IsInteger ? version.LongValue : (long)version.DoubleValue;
        long revisionValue = revision.IsInteger ? revision.LongValue : (long)revision.DoubleValue;
        return (versionValue == 1 && revisionValue == 2)
            || (versionValue == 2 && revisionValue == 3)
            || (versionValue == 4 && revisionValue == 4)
            || (versionValue == 5 && (revisionValue == 5 || revisionValue == 6));
    }

    public static PdfSecurity Create(PdfObject.Dict encrypt, byte[] fileId, string? password, int encryptId)
    {
        if (!IsSupportedRevision(encrypt))
        {
            throw Unsupported();
        }

        int version = ReadInt(encrypt, "V");
        int revision = ReadInt(encrypt, "R");
        bool metadata = ReadEncryptMetadata(encrypt);
        if (version == 1 && revision == 2)
        {
            return CreateRevision2(encrypt, fileId, password, encryptId);
        }

        if (version == 2 && revision == 3)
        {
            return CreateRevision3(encrypt, fileId, password, encryptId);
        }

        if (version == 4 && revision == 4)
        {
            return CreateRevision4(encrypt, fileId, password, encryptId, metadata);
        }

        if (version == 5 && revision == 5)
        {
            return CreateAes256(encrypt, password, encryptId, metadata, stretched: false);
        }

        if (version == 5 && revision == 6)
        {
            return CreateAes256(encrypt, password, encryptId, metadata, stretched: true);
        }

        throw Unsupported();
    }

    public PdfPermissions Permissions => _userPermissions;

    public bool CanModify => _owner || _userPermissions.CanModify;

    public bool CanFill => _owner || _userPermissions.CanFillForms;

    public bool CanAnnotate => _owner || _userPermissions.CanAnnotate;

    public bool UsesInitializationVector(PdfObject.Dict dict)
    {
        CipherKind kind = CipherFor(dict);
        return kind == CipherKind.Aes128 || kind == CipherKind.Aes256;
    }

    public bool EncryptsStringsOf(PdfObject value)
    {
        return _strings != CipherKind.None && !IsExempt(value);
    }

    // Spec 7.4: the signature /Contents is the one plaintext string. It is written into a gap reserved before the digest,
    // and Poppler and pyHanko read it raw. Every other string in a signature dictionary is encrypted (ISO 32000-1 7.6.1).
    public static bool IsSignatureContents(PdfObject.Dict dict, string key)
    {
        if (key != "Contents")
        {
            return false;
        }

        string? type = ReadName(dict, "Type");
        return type == "Sig" || type == "DocTimeStamp";
    }

    public bool EncryptsStream(PdfObject.Dict dict)
    {
        return CipherFor(dict) != CipherKind.None;
    }

    public void DecryptStrings(PdfObject value)
    {
        if (_strings == CipherKind.None || IsExempt(value))
        {
            return;
        }

        Apply(value, value.ObjectNumber, value.Generation);
    }

    // Writes the string ciphertext to destination as Encrypt writes a stream body: through the cipher in bounded chunks,
    // so the working set does not grow with the string.
    public void Encrypt(byte[] plain, int objectNumber, int generation, Stream destination)
    {
        Encrypt(_strings, ObjectKey(objectNumber, generation, _strings), destination, target =>
        {
            for (int offset = 0; offset < plain.Length; offset += StringChunk)
            {
                target.Write(plain, offset, Math.Min(StringChunk, plain.Length - offset));
            }
        });
    }

    public byte[] Decrypt(byte[] cipher, PdfObject.Dict dict)
    {
        return DecryptBytes(cipher, dict.ObjectNumber, dict.Generation, CipherFor(dict));
    }

    public void Encrypt(PdfObject.Dict dict, PdfObject.StreamBody body, Stream destination)
    {
        CipherKind kind = CipherFor(dict);
        Encrypt(kind, ObjectKey(dict.ObjectNumber, dict.Generation, kind), destination, body.CopyTo);
    }

    public Stream OpenDecrypted(Stream ciphertext, PdfObject.Dict dict)
    {
        return new Rc4Stream(ciphertext, ObjectKey(dict.ObjectNumber, dict.Generation, CipherKind.Rc4));
    }

    public Stream OpenDecrypted(Stream ciphertext, byte[] initializationVector, PdfObject.Dict dict)
    {
        CipherKind kind = CipherFor(dict);
        return new DecryptingStream(ciphertext, ObjectKey(dict.ObjectNumber, dict.Generation, kind), initializationVector);
    }

    private static PdfSecurity CreateRevision2(PdfObject.Dict encrypt, byte[] fileId, string? password, int encryptId)
    {
        if (encrypt.Get("Length") is PdfObject.Num length && ReadNumber(length) != 40)
        {
            throw Unsupported();
        }

        if (encrypt.Contains("CF") || encrypt.Contains("StmF") || encrypt.Contains("StrF") || encrypt.Contains("EFF"))
        {
            throw Unsupported();
        }

        return CreateLegacy(encrypt, fileId, password, encryptId, 2, 5, CipherKind.Rc4);
    }

    private static PdfSecurity CreateRevision3(PdfObject.Dict encrypt, byte[] fileId, string? password, int encryptId)
    {
        int bits = encrypt.Get("Length") is PdfObject.Num length ? ReadNumber(length) : 40;
        if (bits < 40 || bits > 128 || (bits % 8) != 0)
        {
            throw Unsupported();
        }

        if (encrypt.Contains("CF") || encrypt.Contains("StmF") || encrypt.Contains("StrF") || encrypt.Contains("EFF"))
        {
            throw Unsupported();
        }

        return CreateLegacy(encrypt, fileId, password, encryptId, 3, bits / 8, CipherKind.Rc4);
    }

    private static PdfSecurity CreateRevision4(PdfObject.Dict encrypt, byte[] fileId, string? password, int encryptId, bool metadata)
    {
        if (encrypt.Get("Length") is PdfObject.Num length && ReadNumber(length) != 128)
        {
            throw Unsupported();
        }

        // ISO 32000: an omitted /StrF or /StmF is Identity, so those strings or streams are not encrypted.
        CipherKind strings = ReadFilter(encrypt, ReadName(encrypt, "StrF") ?? "Identity", 16, revision6: false);
        CipherKind streams = ReadFilter(encrypt, ReadName(encrypt, "StmF") ?? "Identity", 16, revision6: false);
        string? embeddedName = ReadName(encrypt, "EFF");
        CipherKind embedded = embeddedName == null
            ? streams
            : ReadFilter(encrypt, embeddedName, 16, revision6: false);
        return CreateLegacy(encrypt, fileId, password, encryptId, 4, 16, strings, streams, embedded, metadata);
    }

    private static PdfSecurity CreateAes256(PdfObject.Dict encrypt, string? password, int encryptId, bool metadata, bool stretched)
    {
        if (encrypt.Get("Length") is PdfObject.Num length && ReadNumber(length) != 256)
        {
            throw Unsupported();
        }

        CipherKind strings = ReadFilter(encrypt, ReadName(encrypt, "StrF") ?? "Identity", 32, revision6: true);
        CipherKind streams = ReadFilter(encrypt, ReadName(encrypt, "StmF") ?? "Identity", 32, revision6: true);
        string? embeddedName = ReadName(encrypt, "EFF");
        CipherKind embedded = embeddedName == null
            ? streams
            : ReadFilter(encrypt, embeddedName, 32, revision6: true);
        string candidate = password ?? string.Empty;
        byte[] prepared = stretched ? PrepareRevision6(candidate) : PrepareRevision5(candidate);
        byte[] ownerValue = ReadBytes(encrypt, "O", 48);
        byte[] userValue = ReadBytes(encrypt, "U", 48);
        byte[] ownerEncrypted = ReadBytes(encrypt, "OE", 32);
        byte[] userEncrypted = ReadBytes(encrypt, "UE", 32);
        byte[] perms = ReadBytes(encrypt, "Perms", 16);
        int permissions = ReadPermissions(encrypt);
        bool ownerMatched = PasswordMatches(prepared, Slice(ownerValue, 32, 8), userValue, ownerValue, stretched);
        bool userMatched = PasswordMatches(prepared, Slice(userValue, 32, 8), null, userValue, stretched);
        if (!ownerMatched && !userMatched)
        {
            throw password == null ? PasswordRequired() : Rejected();
        }

        byte[] hash = KeyHash(prepared, ownerMatched ? ownerValue : userValue, ownerMatched ? userValue : null, stretched);
        byte[] fileKey = AesCbc(hash, new byte[16], ownerMatched ? Slice(ownerEncrypted, 0, 32) : Slice(userEncrypted, 0, 32), encrypt: false, padding: false);
        ValidatePerms(fileKey, perms, permissions, metadata);
        return new PdfSecurity(fileKey, encryptId, strings, streams, embedded, metadata, ownerMatched, stretched ? 6 : 5, permissions);
    }

    private static PdfSecurity CreateLegacy(
        PdfObject.Dict encrypt,
        byte[] fileId,
        string? password,
        int encryptId,
        int revision,
        int keyBytes,
        CipherKind cipher)
    {
        return CreateLegacy(encrypt, fileId, password, encryptId, revision, keyBytes, cipher, cipher, cipher, metadata: true);
    }

    private static PdfSecurity CreateLegacy(
        PdfObject.Dict encrypt,
        byte[] fileId,
        string? password,
        int encryptId,
        int revision,
        int keyBytes,
        CipherKind strings,
        CipherKind streams,
        CipherKind embedded,
        bool metadata)
    {
        byte[] owner = ReadBytes(encrypt, "O", 32);
        byte[] user = ReadBytes(encrypt, "U", 32);
        int permissions = ReadPermissions(encrypt);
        byte[] padded = PadPassword(PdfTextCodec.EncodePdfDoc(password ?? string.Empty));
        byte[]? recovered = RecoverUserPassword(padded, owner, revision, keyBytes);
        bool ownerMatched = recovered != null && UserMatches(recovered, owner, permissions, fileId, metadata, user, revision, keyBytes);
        bool userMatched = UserMatches(padded, owner, permissions, fileId, metadata, user, revision, keyBytes);
        if (!ownerMatched && !userMatched)
        {
            throw password == null ? PasswordRequired() : Rejected();
        }

        byte[] fileKey = FileKey(ownerMatched ? recovered! : padded, owner, permissions, fileId, metadata, revision, keyBytes);
        bool encryptsMetadata = revision < 4 || metadata;
        return new PdfSecurity(fileKey, encryptId, strings, streams, embedded, encryptsMetadata, ownerMatched, revision, permissions);
    }

    private static bool UserMatches(byte[] paddedPassword, byte[] owner, int permissions, byte[] fileId, bool metadata, byte[] user, int revision, int keyBytes)
    {
        byte[] key = FileKey(paddedPassword, owner, permissions, fileId, metadata, revision, keyBytes);
        if (revision == 2)
        {
            byte[] value = Rc4(key, PasswordPadding);
            return FixedEquals(value, 0, user, 0, 32);
        }

        byte[] hash = UserHash(key, fileId);
        return FixedEquals(hash, 0, user, 0, 16);
    }

    private static byte[] FileKey(byte[] paddedPassword, byte[] owner, int permissions, byte[] fileId, bool metadata, int revision, int keyBytes)
    {
        using IncrementalHash md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5.AppendData(paddedPassword);
        md5.AppendData(owner, 0, 32);
        md5.AppendData(PermissionBytes(permissions));
        md5.AppendData(fileId);
        if (revision >= 4 && !metadata)
        {
            md5.AppendData(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF });
        }

        byte[] hash = md5.GetHashAndReset();
        if (revision >= 3)
        {
            for (int i = 0; i < 50; i++)
            {
                hash = Digest(HashAlgorithmName.MD5, hash, 0, keyBytes);
            }
        }

        var key = new byte[keyBytes];
        Buffer.BlockCopy(hash, 0, key, 0, key.Length);
        return key;
    }

    private static byte[] UserHash(byte[] fileKey, byte[] fileId)
    {
        byte[] hash = Digest(HashAlgorithmName.MD5, PasswordPadding, fileId);
        byte[] result = Rc4(fileKey, hash);
        for (int round = 1; round <= 19; round++)
        {
            result = Rc4(XorKey(fileKey, round), result);
        }

        return result;
    }

    private static byte[]? RecoverUserPassword(byte[] paddedOwner, byte[] ownerValue, int revision, int keyBytes)
    {
        if (ownerValue.Length < 32)
        {
            return null;
        }

        byte[] hash = Digest(HashAlgorithmName.MD5, paddedOwner);
        if (revision >= 3)
        {
            for (int i = 0; i < 50; i++)
            {
                hash = Digest(HashAlgorithmName.MD5, hash);
            }
        }

        var key = new byte[keyBytes];
        Buffer.BlockCopy(hash, 0, key, 0, key.Length);
        var data = new byte[32];
        Buffer.BlockCopy(ownerValue, 0, data, 0, data.Length);
        if (revision == 2)
        {
            return Rc4(key, data);
        }

        for (int round = 19; round >= 0; round--)
        {
            data = Rc4(XorKey(key, round), data);
        }

        return data;
    }

    private static byte[] PadPassword(byte[] password)
    {
        var padded = new byte[32];
        int count = Math.Min(password.Length, padded.Length);
        Buffer.BlockCopy(password, 0, padded, 0, count);
        if (count < padded.Length)
        {
            Buffer.BlockCopy(PasswordPadding, 0, padded, count, padded.Length - count);
        }

        return padded;
    }

    private static bool PasswordMatches(byte[] password, byte[] salt, byte[]? userKey, byte[] expected, bool stretched)
    {
        byte[] hash = stretched ? ComputeHash(password, salt, userKey) : Sha256(password, salt, userKey);
        return FixedEquals(hash, 0, expected, 0, 32);
    }

    private static byte[] KeyHash(byte[] password, byte[] value, byte[]? userKey, bool stretched)
    {
        byte[] salt = Slice(value, 40, 8);
        return stretched ? ComputeHash(password, salt, userKey) : Sha256(password, salt, userKey);
    }

    private static byte[] Sha256(byte[] password, byte[] salt, byte[]? userKey)
    {
        return userKey == null
            ? Digest(HashAlgorithmName.SHA256, password, salt)
            : Digest(HashAlgorithmName.SHA256, password, salt, userKey);
    }

    private static byte[] PrepareRevision5(string password)
    {
        return TruncateUtf8(Encoding.UTF8.GetBytes(password));
    }

    private static byte[] ComputeHash(byte[] password, byte[] salt, byte[]? userKey)
    {
        byte[] user = userKey ?? Array.Empty<byte>();
        byte[] k = Digest(HashAlgorithmName.SHA256, password, salt, user);
        byte[] encrypted = Array.Empty<byte>();
        int round = 0;
        while (round < 64 || (encrypted[encrypted.Length - 1] & 0xFF) > round - 32)
        {
            byte[] block = Concat(password, k, user);
            byte[] repeated = Repeat(block, 64);
            var key = new byte[16];
            var iv = new byte[16];
            Buffer.BlockCopy(k, 0, key, 0, key.Length);
            Buffer.BlockCopy(k, 16, iv, 0, iv.Length);
            encrypted = AesCbc(key, iv, repeated, encrypt: true, padding: false);
            int selector = 0;
            for (int i = 0; i < 16; i++)
            {
                selector += encrypted[i];
            }

            selector %= 3;
            HashAlgorithmName name = selector == 0
                ? HashAlgorithmName.SHA256
                : selector == 1
                    ? HashAlgorithmName.SHA384
                    : HashAlgorithmName.SHA512;
            k = Digest(name, encrypted);
            round++;
        }

        var result = new byte[32];
        Buffer.BlockCopy(k, 0, result, 0, result.Length);
        return result;
    }

    private static void ValidatePerms(byte[] fileKey, byte[] perms, int permissions, bool metadata)
    {
        if (perms.Length != 16)
        {
            throw Unsupported();
        }

        byte[] plain = AesEcb(fileKey, perms, encrypt: false);
        uint stored = (uint)(plain[0] | (plain[1] << 8) | (plain[2] << 16) | (plain[3] << 24));
        if (stored != unchecked((uint)permissions)
            || plain[8] != (metadata ? (byte)'T' : (byte)'F')
            || plain[9] != (byte)'a'
            || plain[10] != (byte)'d'
            || plain[11] != (byte)'b')
        {
            throw Rejected();
        }
    }

    private static byte[] PrepareRevision6(string password)
    {
        var mapped = new StringBuilder(password.Length);
        for (int i = 0; i < password.Length; i++)
        {
            char value = password[i];
            if (char.IsHighSurrogate(value))
            {
                if (i + 1 >= password.Length || !char.IsLowSurrogate(password[i + 1]))
                {
                    throw InvalidRevision6Password();
                }

                mapped.Append(value);
                mapped.Append(password[i + 1]);
                i++;
                continue;
            }

            if (MapsToNothing(value))
            {
                continue;
            }

            mapped.Append(MapsToSpace(value) ? ' ' : value);
        }

        string prepared = mapped.ToString().Normalize(NormalizationForm.FormKC);
        ValidatePrepared(prepared);
        return TruncateUtf8(Encoding.UTF8.GetBytes(prepared));
    }

    private static byte[] TruncateUtf8(byte[] utf8)
    {
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

    private static void ValidatePrepared(string password)
    {
        bool randAL = false;
        bool latin = false;
        for (int i = 0; i < password.Length; i++)
        {
            char value = password[i];
            if (char.IsSurrogate(value))
            {
                if (!char.IsSurrogatePair(password, i))
                {
                    throw InvalidRevision6Password();
                }

                i++;
                continue;
            }

            if (char.IsControl(value) || IsProhibited(value))
            {
                throw InvalidRevision6Password();
            }

            if (IsRandAL(value))
            {
                randAL = true;
            }
            else if (char.IsLetter(value))
            {
                latin = true;
            }
        }

        if (!randAL)
        {
            return;
        }

        if (latin || !IsRandAL(password[0]) || !IsRandAL(password[password.Length - 1]))
        {
            throw InvalidRevision6Password();
        }
    }

    private static bool MapsToNothing(char value)
    {
        return value == '\u00AD'
            || value == '\u034F'
            || value == '\u1806'
            || (value >= '\u180B' && value <= '\u180D')
            || value == '\u200C'
            || value == '\u200D'
            || value == '\u2060'
            || (value >= '\uFE00' && value <= '\uFE0F')
            || value == '\uFEFF';
    }

    private static bool MapsToSpace(char value)
    {
        return value == '\u00A0'
            || value == '\u1680'
            || (value >= '\u2000' && value <= '\u200A')
            || value == '\u202F'
            || value == '\u205F'
            || value == '\u3000';
    }

    private static bool IsProhibited(char value)
    {
        UnicodeCategory category = char.GetUnicodeCategory(value);
        if (category == UnicodeCategory.PrivateUse
            || category == UnicodeCategory.Format
            || category == UnicodeCategory.OtherNotAssigned
            || category == UnicodeCategory.Surrogate)
        {
            return true;
        }

        return (value >= '\uFDD0' && value <= '\uFDEF') || (value & 0xFFFE) == 0xFFFE;
    }

    private static bool IsRandAL(char value)
    {
        return (value >= '\u05D0' && value <= '\u05EA')
            || (value >= '\u05EF' && value <= '\u05F4')
            || (value >= '\u0620' && value <= '\u064A')
            || (value >= '\u066E' && value <= '\u06D5')
            || (value >= '\u06EE' && value <= '\u06EF')
            || (value >= '\u06FA' && value <= '\u06FF')
            || (value >= '\u0750' && value <= '\u077F')
            || (value >= '\u08A0' && value <= '\u08FF')
            || (value >= '\uFB1D' && value <= '\uFB4F')
            || (value >= '\uFB50' && value <= '\uFDFF')
            || (value >= '\uFE70' && value <= '\uFEFC');
    }

    private void Apply(PdfObject value, int objectNumber, int generation)
    {
        if (value is PdfObject.Str text)
        {
            text.Replace(DecryptBytes(text.Raw, objectNumber, generation, _strings));
            return;
        }

        if (value is PdfObject.Arr array)
        {
            foreach (PdfObject item in array.Items)
            {
                Apply(item, objectNumber, generation);
            }

            return;
        }

        if (value is not PdfObject.Dict dict || IsExempt(dict))
        {
            return;
        }

        foreach ((string key, PdfObject item) in dict.Items)
        {
            if (!IsSignatureContents(dict, key))
            {
                Apply(item, objectNumber, generation);
            }
        }
    }

    private bool IsExempt(PdfObject value)
    {
        if (value.ObjectNumber == _encryptId)
        {
            return true;
        }

        if (value is not PdfObject.Dict dict)
        {
            return false;
        }

        return ReadName(dict, "Type") == "XRef";
    }

    private CipherKind CipherFor(PdfObject.Dict dict)
    {
        if (dict.ObjectNumber == _encryptId)
        {
            return CipherKind.None;
        }

        string? type = ReadName(dict, "Type");
        if (type == "XRef")
        {
            return CipherKind.None;
        }

        if (type == "Metadata")
        {
            return _metadata ? _streams : CipherKind.None;
        }

        if (type == "EmbeddedFile")
        {
            return _embeddedFiles;
        }

        return _streams;
    }

    private static void Encrypt(CipherKind kind, byte[] key, Stream destination, Action<Stream> writePlain)
    {
        if (kind == CipherKind.Rc4)
        {
            using (var rc4Guard = new NonClosingStream(destination))
            using (var rc4 = new Rc4Stream(rc4Guard, key))
            {
                writePlain(rc4);
            }

            return;
        }

        byte[] iv = new byte[16];
        using (RandomNumberGenerator random = RandomNumberGenerator.Create())
        {
            random.GetBytes(iv);
        }

        destination.Write(iv, 0, iv.Length);
        using var guard = new NonClosingStream(destination);
        using var aes = new EncryptingStream(guard, key, iv);
        writePlain(aes);
        aes.FlushFinalBlock();
    }

    private byte[] DecryptBytes(byte[] cipher, int objectNumber, int generation, CipherKind kind)
    {
        byte[] key = ObjectKey(objectNumber, generation, kind);
        if (kind == CipherKind.Rc4)
        {
            return Rc4(key, cipher);
        }

        if (cipher.Length < 32 || (cipher.Length & 15) != 0)
        {
            throw new PdfParseException(0, "Encrypted data is truncated.");
        }

        var iv = new byte[16];
        Buffer.BlockCopy(cipher, 0, iv, 0, iv.Length);
        var body = new byte[cipher.Length - iv.Length];
        Buffer.BlockCopy(cipher, iv.Length, body, 0, body.Length);
        return Transform(body, key, iv, encrypt: false);
    }

    private byte[] ObjectKey(int objectNumber, int generation, CipherKind kind)
    {
        if (kind == CipherKind.Aes256)
        {
            return _fileKey;
        }

        bool aes = kind == CipherKind.Aes128;
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

        byte[] hash = Digest(HashAlgorithmName.MD5, _fileKey, extra);
        int length = aes ? 16 : Math.Min(_fileKey.Length + 5, 16);
        var key = new byte[length];
        Buffer.BlockCopy(hash, 0, key, 0, key.Length);
        return key;
    }

    private static byte[] Transform(byte[] data, byte[] key, byte[] iv, bool encrypt)
    {
        try
        {
            return AesCbc(key, iv, data, encrypt, padding: true);
        }
        catch (CryptographicException)
        {
            throw new PdfParseException(0, "Encrypted data could not be decrypted.");
        }
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
        InitializeRc4(key, state);
        var output = new byte[data.Length];
        Buffer.BlockCopy(data, 0, output, 0, data.Length);
        int a = 0;
        int b = 0;
        ProcessRc4(state, ref a, ref b, output, 0, output.Length);
        return output;
    }

    private static void InitializeRc4(byte[] key, byte[] state)
    {
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
    }

    private static void ProcessRc4(byte[] state, ref int a, ref int b, byte[] data, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            a = (a + 1) & 255;
            b = (b + state[a]) & 255;
            byte swap = state[a];
            state[a] = state[b];
            state[b] = swap;
            data[offset + i] = (byte)(data[offset + i] ^ state[(state[a] + state[b]) & 255]);
        }
    }

    private static byte[] XorKey(byte[] key, int round)
    {
        var mixed = new byte[key.Length];
        for (int i = 0; i < key.Length; i++)
        {
            mixed[i] = (byte)(key[i] ^ round);
        }

        return mixed;
    }

    private static byte[] PermissionBytes(int permissions)
    {
        uint value = unchecked((uint)permissions);
        return new[]
        {
            (byte)value,
            (byte)(value >> 8),
            (byte)(value >> 16),
            (byte)(value >> 24),
        };
    }

    private static byte[] Digest(HashAlgorithmName name, params byte[][] parts)
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

    private static byte[] Digest(HashAlgorithmName name, byte[] data, int offset, int count)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(name);
        hash.AppendData(data, offset, count);
        return hash.GetHashAndReset();
    }

    private static byte[] Digest(HashAlgorithmName name, byte[] first, byte[] second)
    {
        return Digest(name, new[] { first, second });
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

    private static byte[] Repeat(byte[] data, int count)
    {
        var output = new byte[data.Length * count];
        for (int i = 0; i < count; i++)
        {
            Buffer.BlockCopy(data, 0, output, i * data.Length, data.Length);
        }

        return output;
    }

    private static byte[] Slice(byte[] data, int offset, int count)
    {
        if (data.Length < offset + count)
        {
            throw Unsupported();
        }

        var slice = new byte[count];
        Buffer.BlockCopy(data, offset, slice, 0, count);
        return slice;
    }

    private static CipherKind ReadFilter(PdfObject.Dict encrypt, string filterName, int keyBytes, bool revision6)
    {
        if (filterName == "Identity")
        {
            return CipherKind.None;
        }

        if (encrypt.Get("CF") is not PdfObject.Dict filters || filters.Get(filterName) is not PdfObject.Dict entry)
        {
            throw Unsupported();
        }

        string? method = ReadName(entry, "CFM");
        if (method == null || method == "None")
        {
            return CipherKind.None;
        }

        CipherKind kind;
        if (method == "V2")
        {
            kind = CipherKind.Rc4;
        }
        else if (method == "AESV2")
        {
            kind = CipherKind.Aes128;
        }
        else if (method == "AESV3")
        {
            kind = CipherKind.Aes256;
        }
        else
        {
            throw Unsupported();
        }

        if (revision6 ? kind != CipherKind.Aes256 : kind == CipherKind.Aes256)
        {
            throw Unsupported();
        }

        string? auth = ReadName(entry, "AuthEvent");
        if (auth == "EFOpen" || (auth != null && auth != "DocOpen"))
        {
            throw Unsupported();
        }

        if (entry.Get("Length") is PdfObject.Num length)
        {
            int value = ReadNumber(length);
            int expectedBytes = kind == CipherKind.Aes256 ? 32 : kind == CipherKind.Aes128 ? 16 : keyBytes;
            if (value != expectedBytes && value != (expectedBytes * 8))
            {
                throw Unsupported();
            }
        }

        return kind;
    }

    private static bool ReadEncryptMetadata(PdfObject.Dict dict)
    {
        return dict.Get("EncryptMetadata") is not PdfObject.Bool flag || flag.Value;
    }

    // ISO 32000-1 Table 21 writes /P as a signed 32-bit integer. Writer quirk (D-57): pypdf writes the same bits
    // unsigned, for example 4294967292 for -4, so a value from 2^31 to 2^32 - 1 is read as those 32 bits. Anything else
    // outside the signed range is refused.
    private static int ReadPermissions(PdfObject.Dict dict)
    {
        if (dict.Get("P") is not PdfObject.Num number || !number.IsInteger)
        {
            throw Unsupported();
        }

        long value = number.LongValue;
        if (value < int.MinValue || value > uint.MaxValue)
        {
            throw Unsupported();
        }

        return unchecked((int)(uint)value);
    }

    private static int ReadInt(PdfObject.Dict dict, string key)
    {
        if (dict.Get(key) is not PdfObject.Num number)
        {
            throw Unsupported();
        }

        return ReadNumber(number);
    }

    private static int ReadNumber(PdfObject.Num number)
    {
        long value = number.IsInteger ? number.LongValue : (long)number.DoubleValue;
        if (value < int.MinValue || value > int.MaxValue)
        {
            throw Unsupported();
        }

        return (int)value;
    }

    private static byte[] ReadBytes(PdfObject.Dict dict, string key, int minimum)
    {
        if (dict.Get(key) is not PdfObject.Str text || text.Raw.Length < minimum)
        {
            throw Unsupported();
        }

        return text.Raw;
    }

    private static string? ReadName(PdfObject.Dict dict, string key)
    {
        return dict.Get(key) is PdfObject.Name name ? name.Value : null;
    }

    private static bool FixedEquals(byte[] left, int leftOffset, byte[] right, int rightOffset, int count)
    {
        if (left.Length < leftOffset + count || right.Length < rightOffset + count)
        {
            return false;
        }

        int diff = 0;
        for (int i = 0; i < count; i++)
        {
            diff |= left[leftOffset + i] ^ right[rightOffset + i];
        }

        return diff == 0;
    }

    private static PdfEncryptedException Unsupported()
    {
        return new PdfEncryptedException("This encryption is not supported.");
    }

    private static PdfEncryptedException PasswordRequired()
    {
        return new PdfEncryptedException("The PDF is encrypted. Pass the password to PdfDocument.Open.");
    }

    private static PdfEncryptedException Rejected()
    {
        return new PdfEncryptedException("The password was rejected.");
    }

    private static PdfEncryptedException InvalidRevision6Password()
    {
        return new PdfEncryptedException("The password is not valid for revision 6 encryption.");
    }

    private sealed class NonClosingStream : Stream
    {
        private readonly Stream _inner;

        public NonClosingStream(Stream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
        }
    }

    private sealed class DecryptingStream : Stream
    {
        private readonly Aes _aes;
        private readonly CryptoStream _crypto;

        public DecryptingStream(Stream source, byte[] key, byte[] iv)
        {
            _aes = Aes.Create();
            _aes.Mode = CipherMode.CBC;
            _aes.Padding = PaddingMode.PKCS7;
            _aes.Key = key;
            _aes.IV = iv;
            _crypto = new CryptoStream(source, _aes.CreateDecryptor(), CryptoStreamMode.Read);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                return _crypto.Read(buffer, offset, count);
            }
            catch (CryptographicException)
            {
                throw new PdfParseException(0, "Encrypted data could not be decrypted.");
            }
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
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _crypto.Dispose();
                _aes.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    // AES-CBC with PKCS#7 padding on write. Whole blocks go through the transform into a fixed buffer, so the working
    // set does not grow with the plaintext; .NET Framework's CryptoStream allocates an output array on every write.
    private sealed class EncryptingStream : Stream
    {
        private readonly Stream _inner;
        private readonly Aes _aes;
        private readonly ICryptoTransform _encryptor;
        private readonly byte[] _chunk = new byte[65536];
        private readonly byte[] _partial = new byte[16];
        private int _partialCount;

        public EncryptingStream(Stream inner, byte[] key, byte[] iv)
        {
            _inner = inner;
            _aes = Aes.Create();
            _aes.Mode = CipherMode.CBC;
            _aes.Padding = PaddingMode.PKCS7;
            _aes.Key = key;
            _aes.IV = iv;
            _encryptor = _aes.CreateEncryptor();
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
            while (count > 0)
            {
                if (_partialCount > 0 || count < _partial.Length)
                {
                    int taken = Math.Min(count, _partial.Length - _partialCount);
                    Buffer.BlockCopy(buffer, offset, _partial, _partialCount, taken);
                    _partialCount += taken;
                    offset += taken;
                    count -= taken;
                    if (_partialCount == _partial.Length)
                    {
                        _inner.Write(_chunk, 0, _encryptor.TransformBlock(_partial, 0, _partial.Length, _chunk, 0));
                        _partialCount = 0;
                    }

                    continue;
                }

                int whole = Math.Min(count - (count % _partial.Length), _chunk.Length);
                _inner.Write(_chunk, 0, _encryptor.TransformBlock(buffer, offset, whole, _chunk, 0));
                offset += whole;
                count -= whole;
            }
        }

        // Pads and writes the last block. Call once, after the last Write.
        public void FlushFinalBlock()
        {
            byte[] last = _encryptor.TransformFinalBlock(_partial, 0, _partialCount);
            _inner.Write(last, 0, last.Length);
            _partialCount = 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _encryptor.Dispose();
                _aes.Dispose();
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class Rc4Stream : Stream
    {
        private readonly Stream _inner;
        private readonly byte[] _state = new byte[256];
        private readonly byte[] _chunk = new byte[65536];
        private int _a;
        private int _b;

        public Rc4Stream(Stream inner, byte[] key)
        {
            _inner = inner;
            InitializeRc4(key, _state);
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _inner.Read(buffer, offset, count);
            if (read > 0)
            {
                ProcessRc4(_state, ref _a, ref _b, buffer, offset, read);
            }

            return read;
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
            while (count > 0)
            {
                int taken = Math.Min(count, _chunk.Length);
                Buffer.BlockCopy(buffer, offset, _chunk, 0, taken);
                ProcessRc4(_state, ref _a, ref _b, _chunk, 0, taken);
                _inner.Write(_chunk, 0, taken);
                offset += taken;
                count -= taken;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
