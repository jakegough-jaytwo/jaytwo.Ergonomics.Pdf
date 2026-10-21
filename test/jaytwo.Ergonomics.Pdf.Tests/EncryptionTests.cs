using System;
using System.IO;
using System.Text;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

public class EncryptionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void password_round_trips_strings_streams_and_the_prefix(bool revision6)
    {
        byte[] pdf = Document(revision6, "user", "owner", -4, textField: true);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, "user", leaveOpen: true))
        {
            Assert.Equal("Secret", document.Metadata.Title);
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Name", "Ada"));
            document.Metadata.Title = "Updated";
            document.Attachments.Add("notes.bin", new MemoryStream(new byte[] { 0, 127, 255 }), leaveOpen: true);
            document.SaveIncremental(stream);
        }

        byte[] saved = stream.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        string appendix = Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.Contains("/Encrypt 20 0 R", appendix, StringComparison.Ordinal);
        Assert.Contains("0102030405060708090A0B0C0D0E0F10", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("(Updated)", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("(Ada)", appendix, StringComparison.Ordinal);

        using var again = PdfDocument.Open(new MemoryStream(saved), "user", leaveOpen: true);
        Assert.Equal("Updated", again.Metadata.Title);
        Assert.Equal("Ada", again.AcroForm.GetFields("Name")[0].Value);
        using Stream notes = again.Attachments["notes.bin"].Open();
        var body = new MemoryStream();
        notes.CopyTo(body);
        Assert.Equal(new byte[] { 0, 127, 255 }, body.ToArray());
    }

    [Fact]
    public void revision6_accepts_the_nfkc_form_of_the_password()
    {
        byte[] pdf = Document(revision6: true, "caf\u00e9", "owner", -4, textField: false);

        using var document = PdfDocument.Open(new MemoryStream(pdf), "cafe\u0301", leaveOpen: true);

        Assert.Equal("Secret", document.Metadata.Title);
    }

    [Fact]
    public void revision5_round_trips_and_keeps_the_password_bytes()
    {
        byte[] pdf = AesDocument(StandardEncryptor.Kind.Revision5, "caf\u00e9", "owner", textField: true);
        using (var owner = PdfDocument.Open(new MemoryStream(pdf), "owner", leaveOpen: true))
        {
            Assert.Equal("Secret", owner.Metadata.Title);
        }

        PdfEncryptedException decomposed = Assert.Throws<PdfEncryptedException>(() => PdfDocument.Open(new MemoryStream(pdf), "cafe\u0301", leaveOpen: true));
        Assert.Contains("rejected", decomposed.Message, StringComparison.OrdinalIgnoreCase);

        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, "caf\u00e9", leaveOpen: true))
        {
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Name", "Ada"));
            document.Metadata.Title = "Updated";
            document.SaveIncremental(stream);
        }

        byte[] saved = stream.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        string appendix = Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.Contains("/Encrypt 20 0 R", appendix, StringComparison.Ordinal);
        Assert.Contains("0102030405060708090A0B0C0D0E0F10", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("(Updated)", appendix, StringComparison.Ordinal);

        using var again = PdfDocument.Open(new MemoryStream(saved), "caf\u00e9", leaveOpen: true);
        Assert.Equal("Updated", again.Metadata.Title);
        Assert.Equal("Ada", again.AcroForm.GetFields("Name")[0].Value);
    }

    [Fact]
    public void revision5_truncates_the_password_to_127_bytes()
    {
        string password = new string('a', 200);
        byte[] pdf = AesDocument(StandardEncryptor.Kind.Revision5, password, "owner", textField: false);

        using (var document = PdfDocument.Open(new MemoryStream(pdf), password, leaveOpen: true))
        {
            Assert.Equal("Secret", document.Metadata.Title);
        }

        using var truncated = PdfDocument.Open(new MemoryStream(pdf), new string('a', 127), leaveOpen: true);
        Assert.Equal("Secret", truncated.Metadata.Title);
        Assert.Throws<PdfEncryptedException>(() => PdfDocument.Open(new MemoryStream(pdf), new string('a', 126), leaveOpen: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void missing_and_wrong_passwords_are_rejected(bool revision6)
    {
        byte[] pdf = Document(revision6, "user", "owner", -4, textField: false);

        PdfEncryptedException missing = Assert.Throws<PdfEncryptedException>(() => PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true));
        PdfEncryptedException wrong = Assert.Throws<PdfEncryptedException>(() => PdfDocument.Open(new MemoryStream(pdf), "nope", leaveOpen: true));
        PdfEncryptedException empty = Assert.Throws<PdfEncryptedException>(() => PdfDocument.Open(new MemoryStream(pdf), string.Empty, leaveOpen: true));

        Assert.Contains("password", missing.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rejected", wrong.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rejected", empty.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void empty_user_password_opens_without_a_password(int revision)
    {
        StandardEncryptor.Kind kind = revision switch
        {
            2 => StandardEncryptor.Kind.Revision2,
            3 => StandardEncryptor.Kind.Revision3,
            4 => StandardEncryptor.Kind.Revision4Aes,
            5 => StandardEncryptor.Kind.Revision5,
            _ => StandardEncryptor.Kind.Revision6,
        };
        byte[] pdf = AesDocument(kind, string.Empty, "owner", textField: false);

        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);

        Assert.Equal("Secret", document.Metadata.Title);
    }

    [Fact]
    public void detected_empty_user_password_keeps_the_permission_bits()
    {
        byte[] pdf = Document(revision6: false, string.Empty, "owner", StandardEncryptor.FillOnly(), textField: true);

        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);

        Assert.False(document.Permissions.CanModify);
        Assert.True(document.Permissions.CanFillForms);
        Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Name", "Ada"));
        Assert.Throws<PdfEncryptionRestrictedException>(() => document.Metadata.Title = "Nope");
    }

    [Fact]
    public void empty_revision4_user_password_opens()
    {
        byte[] pdf = Document(revision6: false, string.Empty, "owner", -4, textField: false);

        using var document = PdfDocument.Open(new MemoryStream(pdf), string.Empty, leaveOpen: true);

        Assert.Equal("Secret", document.Metadata.Title);
    }

    [Fact]
    public void unencrypted_file_ignores_a_password()
    {
        byte[] pdf = PdfBuilder.Basic().Finish();

        using var document = PdfDocument.Open(new MemoryStream(pdf), "unused", leaveOpen: true);

        Assert.Equal(1, document.PageCount);
        AssertPermissions("unencrypted", document.Permissions, true, true, true, true, true, true, true, true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void user_password_is_limited_to_the_permission_bits(bool revision6)
    {
        byte[] pdf = Document(revision6, "user", "owner", StandardEncryptor.FillOnly(), textField: true);
        using var user = PdfDocument.Open(new MemoryStream(pdf), "user", leaveOpen: true);

        Assert.Equal(PdfAppearanceDisposition.Generated, user.AcroForm.Fill("Name", "Ada"));
        Assert.Throws<PdfEncryptionRestrictedException>(() => user.Metadata.Title = "Nope");
        Assert.Throws<PdfEncryptionRestrictedException>(() => user.Attachments.Add("a.bin", new MemoryStream(new byte[] { 1 }), leaveOpen: true));
        Assert.Throws<PdfEncryptionRestrictedException>(() => user.Sign(new PdfSignatureOptions { ReservedContentsBytes = 64 }, new FixedProvider()));

        var ownerStream = new MemoryStream();
        ownerStream.Write(pdf, 0, pdf.Length);
        using (var owner = PdfDocument.Open(ownerStream, "owner", leaveOpen: true))
        {
            owner.Metadata.Title = "Owner";
            owner.SaveIncremental();
        }

        using var saved = PdfDocument.Open(ownerStream, "user", leaveOpen: true);
        Assert.Equal("Owner", saved.Metadata.Title);
    }

    [Fact]
    public void permissions_follow_the_security_revision()
    {
        (string Name, int Revision, int Bits, bool Print, bool High, bool Modify, bool Copy, bool Annotate, bool Fill, bool Assemble, bool Access, bool FillAllowed)[] cases =
        {
            ("r2 none", 2, Reserved(9, 10, 11, 12), false, false, false, false, false, false, false, false, false),
            ("r2 print", 2, Reserved(3, 9, 10, 11, 12), true, true, false, false, false, false, false, false, false),
            ("r2 modify", 2, Reserved(4, 9, 10, 11, 12), false, false, true, false, false, false, true, false, false),
            ("r2 copy", 2, Reserved(5, 9, 10, 11, 12), false, false, false, true, false, false, false, true, false),
            ("r2 annotate", 2, Reserved(6, 9, 10, 11, 12), false, false, false, false, true, true, false, false, true),
            ("r3 none", 3, Reserved(), false, false, false, false, false, false, false, false, false),
            ("r3 print", 3, Reserved(3), true, false, false, false, false, false, false, false, false),
            ("r3 faithful print", 3, Reserved(3, 12), true, true, false, false, false, false, false, false, false),
            ("r3 high quality without print", 3, Reserved(12), false, false, false, false, false, false, false, false, false),
            ("r3 modify", 3, Reserved(4), false, false, true, false, false, false, false, false, false),
            ("r3 assemble", 3, Reserved(11), false, false, false, false, false, false, true, false, false),
            ("r3 copy", 3, Reserved(5), false, false, false, true, false, false, false, true, false),
            ("r3 accessibility", 3, Reserved(10), false, false, false, false, false, false, false, true, false),
            ("r3 annotate", 3, Reserved(6), false, false, false, false, true, true, false, false, true),
            ("r3 fill", 3, Reserved(9), false, false, false, false, false, true, false, false, true),
            ("r4 fill only", 4, StandardEncryptor.FillOnly(), false, false, false, false, false, true, false, false, true),
            ("r5 all", 5, -4, true, true, true, true, true, true, true, true, true),
            ("r6 fill only", 6, StandardEncryptor.FillOnly(), false, false, false, false, false, true, false, false, true),
        };

        foreach ((string name, int revision, int bits, bool print, bool high, bool modify, bool copy, bool annotate, bool fill, bool assemble, bool access, bool fillAllowed) in cases)
        {
            byte[] pdf = Encrypted(KindFor(revision), bits, textField: false);
            using var user = PdfDocument.Open(new MemoryStream(pdf), "user", leaveOpen: true);
            using var owner = PdfDocument.Open(new MemoryStream(pdf), "owner", leaveOpen: true);
            AssertPermissions(name, user.Permissions, print, high, modify, copy, annotate, fill, assemble, access);
            AssertPermissions(name + " owner", owner.Permissions, print, high, modify, copy, annotate, fill, assemble, access);
            if (fillAllowed)
            {
                Assert.Throws<PdfFieldNotFoundException>(() => user.AcroForm.Fill("Name", "Ada"));
            }
            else
            {
                Assert.Throws<PdfEncryptionRestrictedException>(() => user.AcroForm.Fill("Name", "Ada"));
            }

            owner.Metadata.Title = name;
        }
    }

    [Fact]
    public void signature_dictionary_stays_plaintext()
    {
        byte[] pdf = Document(revision6: true, "user", "owner", -4, textField: false);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, "user", leaveOpen: true))
        {
            document.Sign(
                new PdfSignatureOptions
                {
                    ReservedContentsBytes = 32,
                    Reason = "Because",
                    SigningTime = TestCompat.UnixEpoch,
                },
                new FixedProvider());
        }

        string appendix = Encoding.ASCII.GetString(stream.ToArray(), pdf.Length, (int)stream.Length - pdf.Length);
        Assert.Contains("(Because)", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("(Signature1)", appendix, StringComparison.Ordinal);
        using var again = PdfDocument.Open(stream, "user", leaveOpen: true);
        Assert.Contains(again.AcroForm.Fields, field => field.FieldType == PdfFieldType.Signature && field.PartialName == "Signature1");
    }

    [Fact]
    public void clear_metadata_and_identity_attachments_are_not_encrypted()
    {
        var crypt = new StandardEncryptor(true, "user", "owner", -4, encryptMetadata: false);
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.Indirect(4, "<< /Title " + crypt.Text("Secret", 4) + " >>");
        builder.Indirect(20, crypt.Dictionary("/EFF /Identity"));
        byte[] pdf = builder.Finish("/Info 4 0 R /Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, "user", leaveOpen: true))
        {
            document.Metadata.Title = "Visible";
            document.Attachments.Add("open.bin", new MemoryStream(Encoding.ASCII.GetBytes("CLEARTEXT")), leaveOpen: true);
            document.SaveIncremental(stream);
        }

        string appendix = Encoding.ASCII.GetString(stream.ToArray(), pdf.Length, (int)stream.Length - pdf.Length);
        Assert.Contains("xpacket", appendix, StringComparison.Ordinal);
        Assert.Contains("CLEARTEXT", appendix, StringComparison.Ordinal);
        using var again = PdfDocument.Open(stream, "user", leaveOpen: true);
        Assert.Equal("Visible", again.Metadata.Title);
        using Stream open = again.Attachments["open.bin"].Open();
        using var body = new StreamReader(open);
        Assert.Equal("CLEARTEXT", body.ReadToEnd());
    }

    [Fact]
    public void object_stream_strings_are_plaintext_inside_the_encrypted_stream()
    {
        var crypt = new StandardEncryptor(false, "user", "owner", -4, encryptMetadata: true);
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        byte[] payload = Encoding.ASCII.GetBytes("4 0 << /Title (FromStream) >>");
        builder.StreamObject(8, "/Type /ObjStm /N 1 /First 4", crypt.Encrypt(payload, 8, 0));
        builder.Compressed(4, 8, 0);
        builder.Indirect(20, crypt.Dictionary(null));
        builder.AppendXrefStream(21, "/Encrypt 20 0 R /Info 4 0 R /ID [" + crypt.IdLiteral() + "]");

        using var document = PdfDocument.Open(new MemoryStream(builder.Snapshot()), "user", leaveOpen: true);

        Assert.Equal("FromStream", document.Metadata.Title);
    }

    [Fact]
    public void encrypted_xref_stream_is_rejected()
    {
        var crypt = new StandardEncryptor(true, "user", "owner", -4, encryptMetadata: true);
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.Indirect(4, "<< /Title " + crypt.Text("Secret", 4) + " >>");
        builder.Indirect(20, crypt.Dictionary(null));
        builder.AppendXrefStream(21, "/Encrypt 20 0 R /Info 4 0 R /ID [" + crypt.IdLiteral() + "]", data => crypt.Encrypt(data, 21, 0));

        Assert.Throws<PdfParseException>(() => PdfDocument.Open(new MemoryStream(builder.Snapshot()), "user", leaveOpen: true));
    }

    [Fact]
    public void plaintext_xref_stream_opens_with_the_password()
    {
        var crypt = new StandardEncryptor(true, "user", "owner", -4, encryptMetadata: true);
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.Indirect(4, "<< /Title " + crypt.Text("Secret", 4) + " >>");
        builder.Indirect(20, crypt.Dictionary(null));
        builder.AppendXrefStream(21, "/Encrypt 20 0 R /Info 4 0 R /ID [" + crypt.IdLiteral() + "]");

        using var document = PdfDocument.Open(new MemoryStream(builder.Snapshot()), "user", leaveOpen: true);

        Assert.Equal("Secret", document.Metadata.Title);
    }

    [Fact]
    public void plaintext_xref_stream_in_the_tail_does_not_read_the_middle()
    {
        var crypt = new StandardEncryptor(true, "user", "owner", -4, encryptMetadata: true);
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.Indirect(4, "<< /Title " + crypt.Text("Secret", 4) + " >>");
        builder.Ascii(new string('\n', 4096));
        int headLength = (int)builder.Length;
        const int hole = 8 * 1024 * 1024;
        builder.Bytes(new byte[hole]);
        builder.Indirect(20, crypt.Dictionary(null));
        builder.AppendXrefStream(21, "/Encrypt 20 0 R /Info 4 0 R /ID [" + crypt.IdLiteral() + "]");
        int tailLength = (int)builder.Length - headLength - hole;
        if (tailLength < 65536)
        {
            builder.Ascii(new string('\n', 65536 - tailLength));
        }

        byte[] all = builder.Snapshot();
        long tailOffset = headLength + (long)hole;
        var head = new byte[headLength];
        Buffer.BlockCopy(all, 0, head, 0, headLength);
        int tailBytes = (int)(all.Length - tailOffset);
        var tail = new byte[tailBytes];
        Buffer.BlockCopy(all, (int)tailOffset, tail, 0, tailBytes);
        var sparse = new SparsePdfStream(head, tail, tailOffset);

        using var document = PdfDocument.Open(sparse, "user", leaveOpen: true);

        Assert.Equal("Secret", document.Metadata.Title);
        Assert.False(sparse.TouchedHole);
    }

    [Fact]
    public void revision2_fill_in_without_annotate_cannot_fill_a_field()
    {
        byte[] pdf = Rc4Document(StandardEncryptor.Kind.Revision2, 40, textField: true, StandardEncryptor.FillOnly());
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, "user", leaveOpen: true);

        Assert.False(document.Permissions.CanAnnotate);
        Assert.False(document.Permissions.CanFillForms);
        long length = stream.Length;
        Assert.Throws<PdfEncryptionRestrictedException>(() => document.AcroForm.Fill("Name", "Ada"));
        document.SaveIncremental();
        Assert.Equal(length, stream.Length);
    }

    [Theory]
    [InlineData(2, 40)]
    [InlineData(3, 40)]
    [InlineData(3, 128)]
    [InlineData(4, 128)]
    public void rc4_round_trips_strings_streams_and_the_prefix(int revision, int keyBits)
    {
        StandardEncryptor.Kind kind = revision == 2
            ? StandardEncryptor.Kind.Revision2
            : revision == 3
                ? StandardEncryptor.Kind.Revision3
                : StandardEncryptor.Kind.Revision4Rc4;
        byte[] pdf = Rc4Document(kind, keyBits, textField: true);
        using (var owner = PdfDocument.Open(new MemoryStream(pdf), "owner", leaveOpen: true))
        {
            Assert.Equal("Secret", owner.Metadata.Title);
        }

        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, "user", leaveOpen: true))
        {
            Assert.Equal("Secret", document.Metadata.Title);
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Name", "Ada"));
            document.Metadata.Title = "Updated";
            document.Attachments.Add("notes.bin", new MemoryStream(new byte[] { 0, 127, 255 }), leaveOpen: true);
            document.SaveIncremental(stream);
        }

        byte[] saved = stream.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        string appendix = Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.Contains("/Encrypt 20 0 R", appendix, StringComparison.Ordinal);
        Assert.Contains("0102030405060708090A0B0C0D0E0F10", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("(Updated)", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("(Ada)", appendix, StringComparison.Ordinal);

        using var again = PdfDocument.Open(new MemoryStream(saved), "user", leaveOpen: true);
        Assert.Equal("Updated", again.Metadata.Title);
        Assert.Equal("Ada", again.AcroForm.GetFields("Name")[0].Value);
        using Stream notes = again.Attachments["notes.bin"].Open();
        var body = new MemoryStream();
        notes.CopyTo(body);
        Assert.Equal(new byte[] { 0, 127, 255 }, body.ToArray());
    }

    [Fact]
    public void revision4_can_mix_aes_strings_with_rc4_streams()
    {
        var aes = new StandardEncryptor(StandardEncryptor.Kind.Revision4Aes, "user", "owner", -4, encryptMetadata: true);
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.Indirect(4, "<< /Title " + aes.Text("Secret", 4) + " >>");
        builder.Indirect(20, "<< /Filter /Standard /V 4 /R 4 /Length 128 /CF << /Aes << /CFM /AESV2 /Length 16 >> /Rc4 << /CFM /V2 /Length 16 >> >> /StrF /Aes /StmF /Rc4 " + aes.Values() + " >>");
        builder.AppendXrefStream(21, "/Encrypt 20 0 R /Info 4 0 R /ID [" + aes.IdLiteral() + "]");

        using var document = PdfDocument.Open(new MemoryStream(builder.Snapshot()), "user", leaveOpen: true);

        Assert.Equal("Secret", document.Metadata.Title);
    }

    [Fact]
    public void revision4_rc4_stream_decrypts_beside_an_aes_string()
    {
        var aes = new StandardEncryptor(StandardEncryptor.Kind.Revision4Aes, "user", "owner", -4, encryptMetadata: true);
        var plain = new byte[40];
        for (int i = 0; i < plain.Length; i++)
        {
            plain[i] = (byte)((i * 7) + 1);
        }

        byte[] streamCipher = aes.EncryptRc4(plain, 5, 0);
        Assert.NotEqual(aes.Encrypt(plain, 5, 0, new byte[16]), streamCipher);

        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [" + aes.Text("notes.bin", 1) + " 6 0 R] >> >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.Indirect(4, "<< /Title " + aes.Text("Secret", 4) + " >>");
        builder.StreamObject(5, "/Type /EmbeddedFile /Params << /Size 40 >>", streamCipher);
        builder.Indirect(6, "<< /Type /Filespec /F " + aes.Text("notes.bin", 6) + " /UF " + aes.Text("notes.bin", 6) + " /EF << /F 5 0 R /UF 5 0 R >> >>");
        builder.Indirect(20, "<< /Filter /Standard /V 4 /R 4 /Length 128 /CF << /Aes << /CFM /AESV2 /Length 16 >> /Rc4 << /CFM /V2 /Length 16 >> >> /StrF /Aes /StmF /Rc4 " + aes.Values() + " >>");
        builder.AppendXrefStream(21, "/Encrypt 20 0 R /Info 4 0 R /ID [" + aes.IdLiteral() + "]");

        using var document = PdfDocument.Open(new MemoryStream(builder.Snapshot()), "user", leaveOpen: true);

        Assert.Equal("Secret", document.Metadata.Title);
        using Stream notes = document.Attachments["notes.bin"].Open();
        using var body = new MemoryStream();
        notes.CopyTo(body);
        Assert.Equal(plain, body.ToArray());
    }

    [Theory]
    [InlineData("/Filter /Adobe.PubSec /V 4 /R 4")]
    [InlineData("/Filter /Standard /V 1 /R 3 /O <00> /U <00> /P -4")]
    [InlineData("/Filter /Standard /V 5 /R 7 /Length 256 /O <00> /U <00> /P -4")]
    [InlineData("/Filter /Standard /V 4 /R 4 /Length 128 /O <00> /U <00> /P -4 /CF << /StdCF << /CFM /AESV3 /Length 32 >> >> /StmF /StdCF /StrF /StdCF")]
    public void unsupported_handlers_are_refused(string encrypt)
    {
        var builder = PdfBuilder.Basic();
        builder.Indirect(20, "<< " + encrypt + " >>");
        byte[] pdf = builder.Finish("/Encrypt 20 0 R /ID [<0102030405060708090A0B0C0D0E0F10> <0102030405060708090A0B0C0D0E0F10>]");

        Assert.Throws<PdfEncryptedException>(() => PdfDocument.Open(new MemoryStream(pdf), "user", leaveOpen: true));
    }

    [Fact]
    public void failed_open_diagnostic_omits_the_password_and_file_key()
    {
        const string user = "user-secret-q7m";
        const string owner = "owner-secret-q7m";
        const string rejected = "rejected-secret-q7m";
        byte[] fileKey = new byte[32];
        for (int i = 0; i < fileKey.Length; i++)
        {
            fileKey[i] = (byte)(i + 1);
        }

        byte[] pdf = Document(revision6: true, user, owner, -4, textField: false);
        PdfEncryptedException exception = Assert.Throws<PdfEncryptedException>(() => PdfDocument.Open(new MemoryStream(pdf), rejected, leaveOpen: true));
        AssertDiagnosticOmits(exception, rejected, "password");
        AssertDiagnosticOmits(exception, user, "password");
        AssertDiagnosticOmits(exception, owner, "password");
        AssertDiagnosticOmitsKey(exception, fileKey);
    }

    private static void AssertDiagnosticOmits(Exception exception, string secret, string kind)
    {
        Assert.False(string.IsNullOrEmpty(secret));
        string diagnostic = PublicDiagnostic(exception);
        Assert.True(exception.Message.IndexOf(secret, StringComparison.Ordinal) < 0, "The exception message contains the " + kind + ".");
        Assert.True(diagnostic.IndexOf(secret, StringComparison.Ordinal) < 0, "A public diagnostic contains the " + kind + ".");
    }

    private static void AssertDiagnosticOmitsKey(Exception exception, byte[] key)
    {
        Assert.True(key.Length >= 16);
        string hex = TestCompat.ToHexString(key);
        string diagnostic = PublicDiagnostic(exception);
        Assert.True(exception.Message.IndexOf(hex, StringComparison.OrdinalIgnoreCase) < 0, "The exception message contains the key.");
        Assert.True(diagnostic.IndexOf(hex, StringComparison.OrdinalIgnoreCase) < 0, "A public diagnostic contains the key.");
        AssertDiagnosticOmits(exception, Convert.ToBase64String(key), "key");
        AssertDiagnosticOmits(exception, Encoding.GetEncoding(28591).GetString(key), "key");
    }

    private static string PublicDiagnostic(Exception exception)
    {
        var text = new StringBuilder();
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            text.Append(current.Message);
            text.Append('\n');
            text.Append(current.ToString());
            text.Append('\n');
            if (current.Source != null)
            {
                text.Append(current.Source);
                text.Append('\n');
            }

            if (current.HelpLink != null)
            {
                text.Append(current.HelpLink);
                text.Append('\n');
            }

            foreach (object key in current.Data.Keys)
            {
                text.Append(key);
                text.Append('=');
                text.Append(current.Data[key]);
                text.Append('\n');
            }
        }

        return text.ToString();
    }

    private static void AssertPermissions(
        string name,
        PdfPermissions permissions,
        bool print,
        bool highQuality,
        bool modify,
        bool copy,
        bool annotate,
        bool fill,
        bool assemble,
        bool accessibility)
    {
        Assert.True(print == permissions.CanPrint, name + " CanPrint");
        Assert.True(highQuality == permissions.CanPrintHighQuality, name + " CanPrintHighQuality");
        Assert.True(modify == permissions.CanModify, name + " CanModify");
        Assert.True(copy == permissions.CanCopy, name + " CanCopy");
        Assert.True(annotate == permissions.CanAnnotate, name + " CanAnnotate");
        Assert.True(fill == permissions.CanFillForms, name + " CanFillForms");
        Assert.True(assemble == permissions.CanAssemble, name + " CanAssemble");
        Assert.True(accessibility == permissions.CanExtractForAccessibility, name + " CanExtractForAccessibility");
    }

    private static int Reserved(params int[] bits)
    {
        int value = (1 << 6) | (1 << 7);
        for (int bit = 13; bit <= 32; bit++)
        {
            value |= 1 << (bit - 1);
        }

        foreach (int bit in bits)
        {
            value |= 1 << (bit - 1);
        }

        return value;
    }

    private static StandardEncryptor.Kind KindFor(int revision)
    {
        return revision switch
        {
            2 => StandardEncryptor.Kind.Revision2,
            3 => StandardEncryptor.Kind.Revision3,
            4 => StandardEncryptor.Kind.Revision4Aes,
            5 => StandardEncryptor.Kind.Revision5,
            _ => StandardEncryptor.Kind.Revision6,
        };
    }

    private static byte[] Encrypted(StandardEncryptor.Kind kind, int permissions, bool textField)
    {
        var crypt = new StandardEncryptor(kind, "user", "owner", permissions, encryptMetadata: true);
        var builder = new PdfBuilder();
        string form = textField ? " /AcroForm << /Fields [5 0 R] >>" : string.Empty;
        string annots = textField ? " /Annots [5 0 R]" : string.Empty;
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R" + form + " >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792]" + annots + " >>");
        builder.Indirect(4, "<< /Title " + crypt.Text("Secret", 4) + " >>");
        if (textField)
        {
            builder.Indirect(5, "<< /Subtype /Widget /FT /Tx /T " + crypt.Text("Name", 5) + " /Rect [72 700 240 720] /DA " + crypt.Text("/Helv 0 Tf 0 g", 5) + " /P 3 0 R >>");
        }

        builder.Indirect(20, crypt.Dictionary(null));
        return builder.Finish("/Info 4 0 R /Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
    }

    private static byte[] Document(bool revision6, string user, string owner, int permissions, bool textField)
    {
        var crypt = new StandardEncryptor(revision6, user, owner, permissions, encryptMetadata: true);
        var builder = new PdfBuilder();
        string form = textField ? " /AcroForm << /Fields [5 0 R] >>" : string.Empty;
        string annots = textField ? " /Annots [5 0 R]" : string.Empty;
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R" + form + " >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792]" + annots + " >>");
        builder.Indirect(4, "<< /Title " + crypt.Text("Secret", 4) + " >>");
        if (textField)
        {
            builder.Indirect(5, "<< /Subtype /Widget /FT /Tx /T " + crypt.Text("Name", 5) + " /Rect [72 700 240 720] /DA " + crypt.Text("/Helv 0 Tf 0 g", 5) + " /P 3 0 R >>");
        }

        builder.Indirect(20, crypt.Dictionary(null));
        return builder.Finish("/Info 4 0 R /Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
    }

    private static byte[] AesDocument(StandardEncryptor.Kind kind, string user, string owner, bool textField)
    {
        var crypt = new StandardEncryptor(kind, user, owner, -4, encryptMetadata: true);
        var builder = new PdfBuilder();
        string form = textField ? " /AcroForm << /Fields [5 0 R] >>" : string.Empty;
        string annots = textField ? " /Annots [5 0 R]" : string.Empty;
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R" + form + " >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792]" + annots + " >>");
        builder.Indirect(4, "<< /Title " + crypt.Text("Secret", 4) + " >>");
        if (textField)
        {
            builder.Indirect(5, "<< /Subtype /Widget /FT /Tx /T " + crypt.Text("Name", 5) + " /Rect [72 700 240 720] /DA " + crypt.Text("/Helv 0 Tf 0 g", 5) + " /P 3 0 R >>");
        }

        builder.Indirect(20, crypt.Dictionary(null));
        return builder.Finish("/Info 4 0 R /Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
    }

    private static byte[] Rc4Document(StandardEncryptor.Kind kind, int keyBits, bool textField, int permissions = -4)
    {
        var crypt = new StandardEncryptor(kind, "user", "owner", permissions, encryptMetadata: true, keyBits);
        var builder = new PdfBuilder();
        string form = textField ? " /AcroForm << /Fields [5 0 R] >>" : string.Empty;
        string annots = textField ? " /Annots [5 0 R]" : string.Empty;
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R" + form + " >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792]" + annots + " >>");
        builder.Indirect(4, "<< /Title " + crypt.Text("Secret", 4) + " >>");
        if (textField)
        {
            builder.Indirect(5, "<< /Subtype /Widget /FT /Tx /T " + crypt.Text("Name", 5) + " /Rect [72 700 240 720] /DA " + crypt.Text("/Helv 0 Tf 0 g", 5) + " /P 3 0 R >>");
        }

        builder.Indirect(20, crypt.Dictionary(null));
        return builder.Finish("/Info 4 0 R /Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
    }

    private sealed class FixedProvider : IPdfSignatureProvider
    {
        public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

        public byte[] CreateContents(PdfSignatureContext context)
        {
            return new byte[] { 1, 2, 3, 4 };
        }
    }
}
