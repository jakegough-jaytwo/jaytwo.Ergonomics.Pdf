using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace jaytwo.Pdf.Tests;

// Spec W1, W2, and R6: one typed fill, an all-or-nothing bulk fill, and an export of every terminal field. pypdf
// (Fixtures/form_values.py) and qpdf --check read the results independently.
public class FormDataTests
{
    private static readonly Dictionary<string, PdfFieldValue> Filled = new Dictionary<string, PdfFieldValue>
    {
        ["Name"] = "Ada",
        ["Address"] = "1 Main St",
        ["Agree"] = true,
        ["Size"] = "Large",
        ["Color"] = "Blue",
        ["Tags"] = new[] { "A", "C" },
        ["Locked"] = "Fixed",
        ["Group.Child"] = "Nested",
        ["Twin"] = false,
    };

    private static readonly Dictionary<string, PdfFieldValue> Blank = new Dictionary<string, PdfFieldValue>
    {
        ["Name"] = string.Empty,
        ["Address"] = string.Empty,
        ["Agree"] = false,
        ["Size"] = PdfFieldValue.None,
        ["Color"] = PdfFieldValue.None,
        ["Tags"] = PdfFieldValue.FromList(Array.Empty<string>()),
        ["Locked"] = "Fixed",
        ["Group.Child"] = string.Empty,
        ["Twin"] = false,
    };

    private static readonly Dictionary<string, (string Name, PdfFieldValue Value, Type Error)> Refusals = new (string Case, string Name, PdfFieldValue Value, Type Error)[]
    {
        ("unknown name", "Missing", "x", typeof(PdfFieldNotFoundException)),
        ("text takes text", "Name", true, typeof(PdfFieldValueException)),
        ("text takes a value", "Name", PdfFieldValue.None, typeof(PdfFieldValueException)),
        ("max length", "Name", "123456789", typeof(PdfFieldValueException)),
        ("read-only", "Locked", "x", typeof(PdfFieldReadOnlyException)),
        ("password", "Secret", "x", typeof(PdfFieldValueException)),
        ("signature", "Sig", "x", typeof(PdfFieldValueException)),
        ("push button", "Reset", true, typeof(PdfFieldValueException)),
        ("check box takes a Boolean", "Agree", "Yes", typeof(PdfFieldValueException)),
        ("check box on-states differ", "Twin", true, typeof(PdfFieldValueException)),
        ("radio state not found", "Size", "Huge", typeof(PdfFieldValueException)),
        ("radio takes a name", "Size", true, typeof(PdfFieldValueException)),
        ("unlisted option", "Color", "Green", typeof(PdfFieldValueException)),
        ("several values on single select", "Color", new[] { "Red", "Blue" }, typeof(PdfFieldValueException)),
        ("choice takes export values", "Tags", true, typeof(PdfFieldValueException)),
        ("unlisted option in a list", "Tags", new[] { "A", "Z" }, typeof(PdfFieldValueException)),
    }.ToDictionary(item => item.Case, item => (item.Name, item.Value, item.Error));

    public static IEnumerable<object[]> RefusalNames => Refusals.Keys.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(RefusalNames))]
    public void bulk_fill_with_one_invalid_value_changes_nothing(string refusal)
    {
        (string name, PdfFieldValue value, Type error) = Refusals[refusal];
        byte[] pdf = SampleForm(filled: false);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();
        var values = new List<KeyValuePair<string, PdfFieldValue>>
        {
            new KeyValuePair<string, PdfFieldValue>("Name", "Grace"),
            new KeyValuePair<string, PdfFieldValue>("Agree", true),
            new KeyValuePair<string, PdfFieldValue>("Size", "Small"),
            new KeyValuePair<string, PdfFieldValue>("Color", "Red"),
            new KeyValuePair<string, PdfFieldValue>("Tags", new[] { "B" }),
            new KeyValuePair<string, PdfFieldValue>("Group.Child", "Kid"),
        };
        values.RemoveAll(item => item.Key == name);
        values.Add(new KeyValuePair<string, PdfFieldValue>(name, value));

        Exception thrown = Assert.ThrowsAny<PdfException>(() => editor.AcroForm.Fill(values));
        Assert.IsType(error, thrown);
        Assert.Equal(Blank, document.AcroForm.Export());
        Assert.False(document.AcroForm.NeedAppearances);
        editor.SaveIncremental();

        Assert.Equal(pdf.Length, stream.Length);
    }

    [Fact]
    public void bulk_fill_rejects_missing_and_repeated_names_without_writing()
    {
        byte[] pdf = SampleForm(filled: false);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        Assert.Throws<ArgumentNullException>(() => editor.AcroForm.Fill(null!));
        Assert.Throws<ArgumentNullException>(() => editor.AcroForm.Fill(null!, "Ada"));
        Assert.Throws<ArgumentException>(() => editor.AcroForm.Fill(new[]
        {
            new KeyValuePair<string, PdfFieldValue>("Name", "Ada"),
            new KeyValuePair<string, PdfFieldValue>(null!, "Ada"),
        }));
        Assert.Throws<ArgumentException>(() => editor.AcroForm.Fill(new[]
        {
            new KeyValuePair<string, PdfFieldValue>("Name", "Ada"),
            new KeyValuePair<string, PdfFieldValue>("Name", "Grace"),
        }));
        Assert.Throws<ArgumentException>(() => PdfFieldValue.FromList(new[] { "A", null! }));
        Assert.Empty(editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue>()));
        editor.SaveIncremental();

        Assert.Equal(pdf.Length, stream.Length);
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void export_reports_one_value_per_terminal_field_as_pypdf_reads_it()
    {
        foreach (bool filled in new[] { false, true })
        {
            byte[] pdf = SampleForm(filled);
            using var document = PdfDocument.Open(new MemoryStream(pdf, writable: false), leaveOpen: true);

            IReadOnlyDictionary<string, PdfFieldValue> export = document.AcroForm.Export();

            Assert.Equal(filled ? Filled : Blank, export);
            Assert.Equal(PdfFieldValueKind.List, export["Tags"].Kind);
            Assert.Equal(PdfFieldValueKind.Boolean, export["Agree"].Kind);
            Assert.Equal(2, document.AcroForm.GetFields("Address").Count);
            Assert.Equal(2, document.AcroForm.GetFields("Size").Count);
            FormValues.AssertAgree(export, FormValues.Pypdf(pdf), filled ? "filled" : "blank");
        }
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void export_round_trips_through_fill_in_both_directions()
    {
        foreach (bool fillBlank in new[] { true, false })
        {
            Dictionary<string, PdfFieldValue> source = fillBlank ? Filled : Blank;
            byte[] pdf = SampleForm(filled: !fillBlank);
            using var stream = new MemoryStream();
            stream.Write(pdf, 0, pdf.Length);
            IReadOnlyDictionary<string, PdfFieldValue> data;
            HashSet<string> readOnly;
            using (var exported = PdfDocument.Open(new MemoryStream(SampleForm(filled: fillBlank), writable: false)))
            {
                data = exported.AcroForm.Export();
                readOnly = new HashSet<string>(exported.AcroForm.Fields.Where(field => field.ReadOnly).Select(field => field.FullName));
            }

            Assert.Equal(source, data);
            using (var document = PdfDocument.Open(stream, leaveOpen: true))
            {
                using var editor = document.Edit();
                IReadOnlyDictionary<string, PdfAppearanceDisposition> result = editor.AcroForm.Fill(data.Where(item => !readOnly.Contains(item.Key)));

                Assert.Equal(data.Count - 1, result.Count);
                Assert.DoesNotContain(PdfAppearanceDisposition.NeedsViewer, result.Values);
                Assert.False(document.AcroForm.NeedAppearances);
                Assert.Equal(source, document.AcroForm.Export());
                editor.SaveIncremental();
            }

            byte[] saved = stream.ToArray();
            Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
            using var again = PdfDocument.Open(new MemoryStream(saved, writable: false), leaveOpen: true);
            Assert.Equal(source, again.AcroForm.Export());
            Assert.Equal(fillBlank, again.AcroForm.GetFields("Size")[1].IsChecked);
            Assert.False(again.AcroForm.GetFields("Size")[0].IsChecked);
            FormValues.AssertQpdfCheck(saved);
            FormValues.AssertAgree(again.AcroForm.Export(), FormValues.Pypdf(saved), fillBlank ? "filled blank" : "cleared");
        }
    }

    [Fact]
    public void bulk_fill_writes_the_same_revision_as_single_fills()
    {
        byte[] pdf = SampleForm(filled: false);
        List<KeyValuePair<string, PdfFieldValue>> values = Filled.Where(item => item.Key != "Locked").ToList();

        var bulk = new MemoryStream();
        bulk.Write(pdf, 0, pdf.Length);
        IReadOnlyDictionary<string, PdfAppearanceDisposition> bulkResult;
        using (var document = PdfDocument.Open(bulk, leaveOpen: true))
        {
            using var editor = document.Edit();
            bulkResult = editor.AcroForm.Fill(values);
            editor.SaveIncremental();
        }

        var single = new MemoryStream();
        single.Write(pdf, 0, pdf.Length);
        var singleResult = new Dictionary<string, PdfAppearanceDisposition>();
        using (var document = PdfDocument.Open(single, leaveOpen: true))
        {
            using var editor = document.Edit();
            foreach (KeyValuePair<string, PdfFieldValue> item in values)
            {
                singleResult[item.Key] = editor.AcroForm.Fill(item.Key, item.Value);
            }

            editor.SaveIncremental();
        }

        Assert.Equal(singleResult, bulkResult);
        Assert.Equal(PdfAppearanceDisposition.ExistingAppearanceSelected, bulkResult["Size"]);
        Assert.Equal(PdfAppearanceDisposition.Generated, bulkResult["Tags"]);
        Assert.Equal(single.ToArray(), bulk.ToArray());
        string appendix = Encoding.ASCII.GetString(bulk.ToArray(), pdf.Length, (int)(bulk.Length - pdf.Length));
        Assert.Equal(1, Count(appendix, "startxref"));
    }

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData("user", false, true)]
    [InlineData("owner", false, true)]
    [InlineData("user", true, false)]
    [InlineData("owner", true, true)]
    public void encrypted_bulk_fill_is_encrypted_and_read_back_by_qpdf_and_pypdf(string password, bool withoutFillBit, bool allowed)
    {
        int permissions = withoutFillBit ? StandardEncryptor.FillOnly() & ~(1 << 8) : StandardEncryptor.FillOnly();
        byte[] pdf = EncryptedForm(permissions);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        var values = new Dictionary<string, PdfFieldValue> { ["Name"] = "Ada", ["Agree"] = true, ["Color"] = "Blue" };
        using (var document = PdfDocument.Open(stream, password, leaveOpen: true))
        {
            using var editor = document.Edit();
            if (!allowed)
            {
                Assert.Throws<PdfEncryptionRestrictedException>(() => editor.AcroForm.Fill(values));
                editor.SaveIncremental();
                Assert.Equal(pdf.Length, stream.Length);
                return;
            }

            IReadOnlyDictionary<string, PdfAppearanceDisposition> result = editor.AcroForm.Fill(values);
            Assert.All(result.Values, disposition => Assert.Equal(PdfAppearanceDisposition.Generated, disposition));
            editor.SaveIncremental();
        }

        byte[] saved = stream.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        string appendix = Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.Contains("/Encrypt 20 0 R", appendix, StringComparison.Ordinal);
        Assert.Contains("0102030405060708090A0B0C0D0E0F10", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("(Ada)", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("(Blue)", appendix, StringComparison.Ordinal);
        using var again = PdfDocument.Open(new MemoryStream(saved, writable: false), "user", leaveOpen: true);
        Assert.Equal(values, again.AcroForm.Export());
        FormValues.AssertQpdfCheck(saved, "user");
        FormValues.AssertAgree(again.AcroForm.Export(), FormValues.Pypdf(saved, "user"), password);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void docmdp_permission_decides_a_bulk_fill(int permission, bool allowed)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Perms << /DocMDP 9 0 R >> /AcroForm << /Fields [4 0 R 5 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 5 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 100 20] /P 3 0 R >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Tx /T (City) /Rect [0 30 100 50] /P 3 0 R >>");
        builder.Indirect(9, "<< /Type /Sig /Reference [10 0 R] >>");
        builder.Indirect(10, "<< /TransformMethod /DocMDP /TransformParams << /P " + permission.ToString() + " /V /1.2 >> >>");
        byte[] pdf = builder.Finish();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();
        var values = new Dictionary<string, PdfFieldValue> { ["Name"] = "Ada", ["City"] = "Paris" };

        if (allowed)
        {
            Assert.Equal(2, editor.AcroForm.Fill(values).Count);
            Assert.Equal(values, document.AcroForm.Export());
            editor.SaveIncremental();
            Assert.True(stream.Length > pdf.Length);
        }
        else
        {
            Assert.Throws<PdfCertificationLockedException>(() => editor.AcroForm.Fill(values));
            editor.SaveIncremental();
            Assert.Equal(pdf.Length, stream.Length);
        }
    }

    [Fact]
    public void a_fieldmdp_lock_on_one_entry_refuses_the_whole_bulk_fill()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R 6 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 5 0 R 6 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Open) /Rect [0 0 100 20] /P 3 0 R >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Tx /T (Locked) /Rect [0 30 100 50] /P 3 0 R >>");
        builder.Indirect(6, "<< /Subtype /Widget /FT /Sig /T (Sig) /V 9 0 R /Rect [0 60 10 70] /P 3 0 R >>");
        builder.Indirect(9, "<< /Type /Sig /Reference [10 0 R] >>");
        builder.Indirect(10, "<< /TransformMethod /FieldMDP /TransformParams << /Action /Include /Fields [(Locked)] /V /1.2 >> >>");
        byte[] pdf = builder.Finish();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        Assert.Throws<PdfCertificationLockedException>(() => editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue> { ["Open"] = "Ada", ["Locked"] = "Ada" }));
        editor.SaveIncremental();
        Assert.Equal(pdf.Length, stream.Length);

        editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue> { ["Open"] = "Ada" });
        Assert.Equal("Ada", document.AcroForm.Export()["Open"].Text);
        Assert.Equal(string.Empty, document.AcroForm.Export()["Locked"].Text);
    }

    [Fact]
    public void bulk_fill_of_500_fields_on_1000_pages_does_not_read_content_holes()
    {
        const int Pages = 1000;
        const int Fields = 500;
        const int FirstField = Pages + 3;
        const int FirstContents = FirstField + Fields;
        var builder = new PdfBuilder();
        var kids = new StringBuilder();
        var fields = new StringBuilder();
        for (int page = 0; page < Pages; page++)
        {
            kids.Append(page + 3).Append(" 0 R ");
        }

        for (int field = 0; field < Fields; field++)
        {
            fields.Append(FirstField + field).Append(" 0 R ");
        }

        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [" + fields.ToString() + "] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count " + Pages.ToString() + " /Kids [" + kids.ToString() + "] /MediaBox [0 0 612 792] >>");
        for (int page = 0; page < Pages; page++)
        {
            string annots = page % 2 == 0 ? " /Annots [" + (FirstField + (page / 2)).ToString() + " 0 R]" : string.Empty;
            builder.Indirect(page + 3, "<< /Type /Page /Parent 2 0 R /Contents " + (FirstContents + page).ToString() + " 0 R" + annots + " >>");
        }

        for (int field = 0; field < Fields; field++)
        {
            builder.Indirect(FirstField + field, "<< /Subtype /Widget /FT /Tx /T (F" + field.ToString() + ") /Rect [72 700 272 720] /P " + ((field * 2) + 3).ToString() + " 0 R >>");
        }

        long hole = builder.Length + 100;
        for (int page = 0; page < Pages; page++)
        {
            builder.Hole(FirstContents + page, hole);
        }

        (byte[] head, byte[] tail) = builder.Split(builder.Length + 2000000, string.Empty);
        var sparse = new SparsePdfStream(head, tail, head.Length + 2000000);
        using (var document = PdfDocument.Open(sparse, leaveOpen: true))
        {
            using var editor = document.Edit();
            var values = new Dictionary<string, PdfFieldValue>();
            for (int field = 0; field < Fields; field++)
            {
                values["F" + field.ToString()] = "Value " + field.ToString();
            }

            IReadOnlyDictionary<string, PdfAppearanceDisposition> result = editor.AcroForm.Fill(values);

            Assert.Equal(Fields, result.Count);
            Assert.All(result.Values, disposition => Assert.Equal(PdfAppearanceDisposition.Generated, disposition));
            editor.SaveIncremental(sparse);
        }

        Assert.False(sparse.TouchedHole);
        using var again = PdfDocument.Open(sparse, leaveOpen: true);
        IReadOnlyDictionary<string, PdfFieldValue> export = again.AcroForm.Export();
        Assert.Equal(Fields, export.Count);
        Assert.Equal("Value 499", export["F499"].Text);
        Assert.Equal(999, again.AcroForm.GetFields("F499")[0].PageNumber);
        Assert.False(sparse.TouchedHole);
    }

    // The checks never read /DR, so a broken /DR fails the text appearance after the check box was already edited.
    [Fact]
    public void a_failure_while_applying_restores_every_edit()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R] /DR 9 0 R >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> /Annots [4 0 R 5 0 R] >>");
        builder.Indirect(4, "<< /Type /Annot /Subtype /Widget /FT /Btn /T (Agree) /Rect [72 640 86 654] /P 3 0 R >>");
        builder.Indirect(5, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /Rect [72 700 272 720] /P 3 0 R >>");
        builder.Hole(9, builder.OffsetOf(1));
        byte[] pdf = builder.Finish();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();
        var values = new List<KeyValuePair<string, PdfFieldValue>>
        {
            new KeyValuePair<string, PdfFieldValue>("Agree", true),
            new KeyValuePair<string, PdfFieldValue>("Name", "Ada"),
        };

        Assert.Throws<PdfParseException>(() => editor.AcroForm.Fill(values));
        Assert.False(document.AcroForm.Export()["Agree"].Boolean);
        Assert.Equal(string.Empty, document.AcroForm.Export()["Name"].Text);
        editor.SaveIncremental();
        Assert.Equal(pdf.Length, stream.Length);

        Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("Agree", true));
        editor.SaveIncremental();
        Assert.True(stream.Length > pdf.Length);
    }

    [Fact]
    public void field_values_compare_by_kind_and_content()
    {
        Assert.Equal(PdfFieldValueKind.None, default(PdfFieldValue).Kind);
        Assert.Equal(PdfFieldValue.None, (PdfFieldValue)(string?)null);
        Assert.Equal(PdfFieldValue.None, (PdfFieldValue)(string[]?)null);
        Assert.Equal(PdfFieldValue.FromText("A"), (PdfFieldValue)"A");
        Assert.NotEqual(PdfFieldValue.FromText("A"), PdfFieldValue.FromList(new[] { "A" }));
        Assert.NotEqual(PdfFieldValue.FromText(string.Empty), PdfFieldValue.None);
        Assert.NotEqual(PdfFieldValue.FromList(Array.Empty<string>()), PdfFieldValue.None);
        Assert.Equal(PdfFieldValue.FromList(new[] { "A", "B" }), (PdfFieldValue)new[] { "A", "B" });
        Assert.Equal(PdfFieldValue.FromList(new[] { "A", "B" }).GetHashCode(), ((PdfFieldValue)new[] { "A", "B" }).GetHashCode());
        Assert.True(((PdfFieldValue)true).Boolean);
        Assert.Null(((PdfFieldValue)true).Text);
        Assert.Empty(((PdfFieldValue)"A").Values);
        Assert.Equal("[A, B]", ((PdfFieldValue)new[] { "A", "B" }).ToString());

        var source = new[] { "A" };
        PdfFieldValue copied = source;
        source[0] = "B";
        Assert.Equal("A", copied.Values[0]);
    }

    // Every field kind R6 names, and the ones it leaves out, on one page. Check boxes and radios have existing
    // on-state appearances; Twin's two widgets advertise different on-state names.
    private static byte[] SampleForm(bool filled)
    {
        string Value(string value) => filled ? " /V " + value : string.Empty;
        const string Widget = "/Type /Annot /Subtype /Widget /P 3 0 R";
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R 8 0 R 9 0 R 12 0 R 13 0 R 14 0 R 15 0 R 16 0 R 17 0 R 18 0 R 20 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> /Annots [4 0 R 6 0 R 7 0 R 8 0 R 10 0 R 11 0 R 12 0 R 13 0 R 14 0 R 15 0 R 16 0 R 17 0 R 19 0 R 21 0 R 22 0 R] >>");
        builder.Indirect(4, "<< " + Widget + " /FT /Tx /T (Name) /MaxLen 8 /DA (/Helv 0 Tf 0 g) /Rect [72 700 272 720]" + Value("(Ada)") + " >>");
        builder.Indirect(5, "<< /FT /Tx /T (Address) /DA (/Helv 0 Tf 0 g) /Kids [6 0 R 7 0 R]" + Value("(1 Main St)") + " >>");
        builder.Indirect(6, "<< " + Widget + " /Parent 5 0 R /Rect [72 670 272 690] >>");
        builder.Indirect(7, "<< " + Widget + " /Parent 5 0 R /Rect [300 670 500 690] >>");
        builder.Indirect(8, "<< " + Widget + " /FT /Btn /T (Agree) /Rect [72 640 86 654] /AP << /N << /Yes 30 0 R /Off 31 0 R >> >>" + (filled ? " /V /Yes /AS /Yes" : " /V /Off /AS /Off") + " >>");
        builder.Indirect(9, "<< /FT /Btn /Ff 49152 /T (Size) /Kids [10 0 R 11 0 R]" + Value("/Large") + " >>");
        builder.Indirect(10, "<< " + Widget + " /Parent 9 0 R /Rect [72 610 86 624] /AP << /N << /Small 30 0 R /Off 31 0 R >> >> /AS /Off >>");
        builder.Indirect(11, "<< " + Widget + " /Parent 9 0 R /Rect [100 610 114 624] /AP << /N << /Large 30 0 R /Off 31 0 R >> >> /AS " + (filled ? "/Large" : "/Off") + " >>");
        builder.Indirect(12, "<< " + Widget + " /FT /Ch /Ff 131072 /T (Color) /Opt [(Red) (Blue)] /DA (/Helv 0 Tf 0 g) /Rect [72 580 272 600]" + Value("(Blue)") + " >>");
        builder.Indirect(13, "<< " + Widget + " /FT /Ch /Ff 2097152 /T (Tags) /Opt [(A) (B) (C)] /DA (/Helv 0 Tf 0 g) /Rect [72 520 272 570]" + Value("[(A) (C)]") + " >>");
        builder.Indirect(14, "<< " + Widget + " /FT /Tx /Ff 1 /T (Locked) /V (Fixed) /Rect [72 490 272 510] >>");
        builder.Indirect(15, "<< " + Widget + " /FT /Tx /Ff 8192 /T (Secret) /Rect [72 460 272 480] >>");
        builder.Indirect(16, "<< " + Widget + " /FT /Sig /T (Sig) /Rect [72 400 272 450] >>");
        builder.Indirect(17, "<< " + Widget + " /FT /Btn /Ff 65536 /T (Reset) /Rect [300 400 360 420] >>");
        builder.Indirect(18, "<< /T (Group) /Kids [19 0 R] >>");
        builder.Indirect(19, "<< " + Widget + " /Parent 18 0 R /FT /Tx /T (Child) /DA (/Helv 0 Tf 0 g) /Rect [72 370 272 390]" + Value("(Nested)") + " >>");
        builder.Indirect(20, "<< /FT /Btn /T (Twin) /Kids [21 0 R 22 0 R] >>");
        builder.Indirect(21, "<< " + Widget + " /Parent 20 0 R /Rect [72 340 86 354] /AP << /N << /Yes 30 0 R /Off 31 0 R >> >> /AS /Off >>");
        builder.Indirect(22, "<< " + Widget + " /Parent 20 0 R /Rect [100 340 114 354] /AP << /N << /On 30 0 R /Off 31 0 R >> >> /AS /Off >>");
        builder.StreamObject(30, "/Type /XObject /Subtype /Form /BBox [0 0 14 14]", Encoding.ASCII.GetBytes("0 g 3 3 8 8 re f"));
        builder.StreamObject(31, "/Type /XObject /Subtype /Form /BBox [0 0 14 14]", Array.Empty<byte>());
        return builder.Finish();
    }

    // A revision 6 (AES-256) form with a text field, a check box with no appearance, and a choice field.
    private static byte[] EncryptedForm(int permissions)
    {
        var crypt = new StandardEncryptor(StandardEncryptor.Kind.Revision6, "user", "owner", permissions, encryptMetadata: true);
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R 6 0 R 7 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> /Annots [5 0 R 6 0 R 7 0 R] >>");
        builder.Indirect(5, "<< /Type /Annot /Subtype /Widget /FT /Tx /T " + crypt.Text("Name", 5) + " /Rect [72 700 240 720] /DA " + crypt.Text("/Helv 0 Tf 0 g", 5) + " /P 3 0 R >>");
        builder.Indirect(6, "<< /Type /Annot /Subtype /Widget /FT /Btn /T " + crypt.Text("Agree", 6) + " /Rect [72 670 86 684] /P 3 0 R >>");
        builder.Indirect(7, "<< /Type /Annot /Subtype /Widget /FT /Ch /Ff 131072 /T " + crypt.Text("Color", 7) + " /Opt [" + crypt.Text("Red", 7) + " " + crypt.Text("Blue", 7) + "] /Rect [72 640 240 660] /DA " + crypt.Text("/Helv 0 Tf 0 g", 7) + " /P 3 0 R >>");
        builder.Indirect(20, crypt.Dictionary(null));
        return builder.Finish("/Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
    }

    private static int Count(string text, string value)
    {
        int count = 0;
        for (int index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
