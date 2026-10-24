using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace jaytwo.Pdf.Tests;

public class AcroFormTests
{
    [Fact]
    public void empty_form_has_no_fields()
    {
        byte[] pdf = PdfBuilder.Basic().Finish();

        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);

        Assert.False(document.AcroForm.HasFields);
        Assert.Empty(document.AcroForm.Fields);
        Assert.Equal(1, document.PageCount);
    }

    [Fact]
    public void merged_split_radio_and_inherited_fields_report_geometry()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 6 0 R 8 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] /MediaBox [0 0 500 600] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /Rotate 90 /CropBox [5 6 105 206] /Annots [5 0 R 7 0 R 9 0 R 10 0 R] >>");
        builder.Indirect(4, "<< /FT /Tx /T (Person) /V (Ada) /Ff 4096 /DA (/Helv 12 Tf 0 0 1 rg) /Kids [5 0 R] >>");
        builder.Indirect(5, "<< /Subtype /Widget /Rect [10 20 110 40] >>");
        builder.Indirect(6, "<< /T (Address) /Kids [7 0 R] >>");
        builder.Indirect(7, "<< /Subtype /Widget /FT /Tx /T (City) /Rect [1 2 3 4] /P 3 0 R >>");
        builder.Indirect(8, "<< /FT /Btn /Ff 32768 /T (Color) /V /Red /Kids [9 0 R 10 0 R] >>");
        builder.Indirect(9, "<< /Subtype /Widget /Rect [0 0 12 12] /AP << /N << /Off null /Red null >> >> >>");
        builder.Indirect(10, "<< /Subtype /Widget /Rect [0 20 12 32] /AP << /N << /Off null /Blue null >> >> >>");
        byte[] pdf = builder.Finish();

        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);
        PdfField person = Assert.Single(document.AcroForm.Fields, field => field.FullName == "Person");
        PdfField city = Assert.Single(document.AcroForm.Fields, field => field.FullName == "Address.City");
        Assert.DoesNotContain(document.AcroForm.Fields, field => field.FullName == "Address");
        PdfField red = document.AcroForm.Fields[IndexOf(document, "Color", 0)];
        PdfField blue = document.AcroForm.Fields[IndexOf(document, "Color", 1)];

        Assert.Equal("Ada", person.Value);
        Assert.True(person.Multiline);
        Assert.Equal(1, person.PageNumber);
        Assert.Equal(90, person.PageRotation);
        Assert.Equal(500, person.MediaBox.Width);
        Assert.Equal(100, person.Rect.Width);
        Assert.Equal("City", city.PartialName);
        Assert.Equal(1, city.Rect.Llx);
        Assert.Equal("Red", red.ExportValue);
        Assert.Equal("Blue", blue.ExportValue);
        Assert.True(red.IsChecked);
        Assert.False(blue.IsChecked);
        Assert.Equal(PdfButtonKind.Radio, red.ButtonKind);
        Assert.Equal(0, red.WidgetIndex);
        Assert.Equal(1, blue.WidgetIndex);
        Assert.Equal(4, document.AcroForm.Fields.Count);
        Assert.Equal("Red", document.AcroForm.GetFields("Color")[0].ExportValue);
        PdfPage page = Assert.Single(document.Pages);
        Assert.Equal(1, page.Number);
        Assert.Equal(90, page.Rotation);
        Assert.Equal(500, page.MediaBox.Width);
        Assert.Equal(600, page.MediaBox.Height);
        Assert.Equal(new PdfRect(5, 6, 105, 206), page.CropBox);
        Assert.Equal(page.MediaBox, person.MediaBox);
    }

    [Fact]
    public void radio_button_opt_does_not_replace_the_on_state_contract()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R 6 0 R] >>");
        builder.Indirect(4, "<< /FT /Btn /Ff 32768 /T (Color) /Opt [(Red) (Blue)] /V /1 /Kids [5 0 R 6 0 R] >>");
        builder.Indirect(5, "<< /Subtype /Widget /Parent 4 0 R /Rect [10 10 30 30] /AP << /N << /Off null /0 null >> >> /P 3 0 R >>");
        builder.Indirect(6, "<< /Subtype /Widget /Parent 4 0 R /Rect [40 10 60 30] /AP << /N << /Off null /1 null >> >> /P 3 0 R >>");
        using var document = PdfDocument.Open(new MemoryStream(builder.Finish()), leaveOpen: true);
        using var editor = document.Edit();

        var fields = document.AcroForm.GetFields("Color");
        Assert.Equal("0", fields[0].ExportValue);
        Assert.Equal("1", fields[1].ExportValue);
        Assert.True(fields[1].IsChecked);
        Assert.Equal(PdfAppearanceDisposition.ExistingAppearanceSelected, editor.AcroForm.Fill("Color", "0"));
    }

    [Fact]
    public void simple_text_generates_helvetica_and_other_values_request_appearances()
    {
        byte[] pdf = Form(
            "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [72 700 300 724] /DA (/Helv 0 Tf 0 g) /MaxLen 8 /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Tx /T (Notes) /Rect [72 640 300 700] /Ff 4096 /Q 1 /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Tx /T (Secret) /Rect [72 600 300 620] /Ff 8192 /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Tx /T (Locked) /Rect [72 560 300 580] /Ff 1 /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Tx /T (Comb) /Rect [72 520 300 540] /Ff 16777216 /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Sig /T (Sig) /Rect [72 480 300 500] /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Btn /T (Agree) /Rect [72 440 90 458] /AP << /N << /Off null /Checked null >> >> /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Btn /T (Bare) /Rect [72 400 90 418] /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Ch /T (Choice) /Rect [72 360 200 380] /P 3 0 R >>");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        long length = stream.Length;
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.False(Field(document, "Agree").IsChecked);
            Assert.False(Field(document, "Bare").IsChecked);
            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("Name", "Ada"));
            Assert.False(document.AcroForm.NeedAppearances);
            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("Notes", "one\ntwo"));
            Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Name", "too-long!"));
            Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Secret", "nope"));
            Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Sig", "nope"));
            Assert.Throws<PdfFieldReadOnlyException>(() => editor.AcroForm.Fill("Locked", "nope"));
            Assert.Throws<PdfFieldNotFoundException>(() => editor.AcroForm.Fill("Missing", "nope"));
            Assert.Throws<PdfFieldNotFoundException>(() => document.AcroForm.GetFields("Missing"));
            Assert.Equal("Name", document.AcroForm.GetFields("Name")[0].PartialName);
            Assert.Equal(PdfAppearanceDisposition.NeedsViewer, editor.AcroForm.Fill("Comb", "AB"));
            Assert.Equal(PdfAppearanceDisposition.NeedsViewer, editor.AcroForm.Fill("Name", "日本語"));
            Assert.True(document.AcroForm.NeedAppearances);
            Assert.Equal(PdfAppearanceDisposition.ExistingAppearanceSelected, editor.AcroForm.Fill("Agree", true));
            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("Bare", true));
            Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Choice", "Yes"));
            editor.SaveIncremental(stream);
        }

        Assert.True(stream.Length > length);
        byte[] saved = stream.ToArray();
        string appendix = Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.Contains("/Helv", appendix, StringComparison.Ordinal);
        Assert.Contains("/Helvetica", appendix, StringComparison.Ordinal);
        using var again = PdfDocument.Open(new MemoryStream(saved), leaveOpen: true);
        Assert.Equal("日本語", Field(again, "Name").Value);
        Assert.Null(Field(again, "Choice").Value);
        Assert.Equal("Checked", Field(again, "Agree").Value);
        Assert.True(Field(again, "Agree").IsChecked);
        Assert.True(Field(again, "Bare").IsChecked);
        Assert.True(again.AcroForm.NeedAppearances);
    }

    [Fact]
    public void maxlen_overflow_does_not_dirty_the_document()
    {
        byte[] pdf = Form("<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 10 10] /MaxLen 3 /P 3 0 R >>");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Name", "abcd"));
        editor.SaveIncremental(stream);

        Assert.Equal(pdf.Length, stream.Length);
    }

    [Fact]
    public void docmdp_blocks_writes_and_still_allows_reads()
    {
        byte[] pdf = Build(
            new[] { "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 10 10] /P 3 0 R >>" },
            " /Perms << /DocMDP 9 0 R >>");
        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);
        using var editor = document.Edit();

        Assert.Equal("Name", document.AcroForm.Fields[0].FullName);
        Assert.Throws<PdfCertificationLockedException>(() => editor.AcroForm.Fill("Name", "Ada"));
        Assert.Throws<PdfCertificationLockedException>(() => editor.Sign(new MemoryStream(), new PdfSignatureOptions(), new FixedProvider()));
    }

    [Fact]
    public void docmdp_form_fill_permission_allows_fill_and_signing_but_not_metadata()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Perms << /DocMDP 9 0 R >> /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 100 20] /P 3 0 R >>");
        builder.Indirect(9, "<< /Type /Sig /Reference [10 0 R] >>");
        builder.Indirect(10, "<< /TransformMethod /DocMDP /TransformParams << /P 2 /V /1.2 >> >>");
        byte[] pdf = builder.Finish();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("Name", "Ada"));
        Assert.Throws<PdfCertificationLockedException>(() => editor.Metadata.Title = "Nope");
        editor.Sign(new MemoryStream(), new PdfSignatureOptions { ReservedContentsBytes = 32 }, new FixedProvider());
    }

    [Fact]
    public void signature_field_reference_certification_blocks_writes_without_catalog_perms()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Sig /T (Certification) /V 9 0 R /Rect [0 0 10 10] /P 3 0 R >>");
        builder.Indirect(9, "<< /Type /Sig /Reference [10 0 R] >>");
        builder.Indirect(10, "<< /TransformMethod /DocMDP /TransformParams << /P 1 /V /1.2 >> >>");
        using var document = PdfDocument.Open(new MemoryStream(builder.Finish()), leaveOpen: true);
        using var editor = document.Edit();

        Assert.Throws<PdfCertificationLockedException>(() => editor.Metadata.Title = "Blocked");
        Assert.Throws<PdfCertificationLockedException>(() => editor.Attachments.Add("blocked.txt", new MemoryStream(new byte[] { 1 }), leaveOpen: false));
    }

    [Fact]
    public void refused_write_keeps_the_cached_certification_answer()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Perms << /DocMDP 9 0 R >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        builder.Indirect(9, "<< /Type /Sig /Reference [10 0 R] >>");
        builder.Indirect(10, "<< /TransformMethod /DocMDP /TransformParams << /P 1 /V /1.2 >> >>");
        byte[] pdf = builder.Finish();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        Assert.Throws<PdfCertificationLockedException>(() => editor.Metadata.Title = "First");
        Assert.Throws<PdfCertificationLockedException>(() => editor.Metadata.Title = "Second");
        editor.SaveIncremental();
        Assert.Equal(pdf, stream.ToArray());
    }

    [Fact]
    public void fieldmdp_all_blocks_a_fill_and_still_allows_a_read()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 5 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 100 20] /P 3 0 R >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Sig /T (Sig) /V 9 0 R /Rect [0 0 10 10] /P 3 0 R >>");
        builder.Indirect(9, "<< /Type /Sig /Reference [10 0 R] >>");
        builder.Indirect(10, "<< /TransformMethod /FieldMDP /TransformParams << /Action /All /V /1.2 >> >>");
        using var document = PdfDocument.Open(new MemoryStream(builder.Finish()), leaveOpen: true);
        using var editor = document.Edit();

        Assert.Equal("Name", document.AcroForm.GetFields("Name")[0].FullName);
        Assert.Throws<PdfCertificationLockedException>(() => editor.AcroForm.Fill("Name", "Ada"));
    }

    [Fact]
    public void fieldmdp_include_locks_only_the_named_fields()
    {
        using var document = PdfDocument.Open(new MemoryStream(FieldMdpForm("/Include /Fields [(Locked)]")), leaveOpen: true);
        using var editor = document.Edit();

        Assert.Throws<PdfCertificationLockedException>(() => editor.AcroForm.Fill("Locked", "Ada"));
        editor.AcroForm.Fill("Open", "Ada");
        Assert.Equal("Ada", document.AcroForm.GetFields("Open")[0].Value);
    }

    [Fact]
    public void fieldmdp_exclude_locks_every_field_except_the_named_fields()
    {
        using var document = PdfDocument.Open(new MemoryStream(FieldMdpForm("/Exclude /Fields [(Open)]")), leaveOpen: true);
        using var editor = document.Edit();

        Assert.Throws<PdfCertificationLockedException>(() => editor.AcroForm.Fill("Locked", "Ada"));
        editor.AcroForm.Fill("Open", "Ada");
        Assert.Equal("Ada", document.AcroForm.GetFields("Open")[0].Value);
    }

    [Theory]
    [InlineData("/Include /Fields [(Group.Child)]", false, false, true)]
    [InlineData("/Exclude /Fields [(Group.Child)]", true, true, false)]
    [InlineData("/Include /Fields [(Child) (Gro) (Group.Chil) (group.child)]", false, false, false)]
    [InlineData("/Exclude /Fields [(Child) (Gro) (Group.Chil) (group.child)]", true, true, true)]
    [InlineData("/Include /Fields []", false, false, false)]
    [InlineData("/Exclude /Fields []", true, true, true)]
    [InlineData("/Include /Fields [(Group)]", false, false, true)]
    [InlineData("/Exclude /Fields [(Group)]", true, true, false)]
    [InlineData("/Unknown /Fields [(Open)]", true, true, true)]
    public void fieldmdp_matches_fully_qualified_field_names_only(string actionAndFields, bool lockedLocked, bool openLocked, bool childLocked)
    {
        using var document = PdfDocument.Open(new MemoryStream(NestedFieldMdpForm(actionAndFields, certified: false)), leaveOpen: true);
        using var editor = document.Edit();

        AssertFill(document, editor, "Locked", lockedLocked);
        AssertFill(document, editor, "Open", openLocked);
        AssertFill(document, editor, "Group.Child", childLocked);
    }

    [Theory]
    [InlineData("/Include /Fields [(Locked)]", true, false, false)]
    [InlineData("/Exclude /Fields [(Open) (Group.Child)]", true, false, false)]
    [InlineData("/All", true, true, true)]
    public void docmdp_form_fill_permission_with_fieldmdp_applies_both(string actionAndFields, bool lockedLocked, bool openLocked, bool childLocked)
    {
        using var document = PdfDocument.Open(new MemoryStream(NestedFieldMdpForm(actionAndFields, certified: true)), leaveOpen: true);
        using var editor = document.Edit();

        AssertFill(document, editor, "Locked", lockedLocked);
        AssertFill(document, editor, "Open", openLocked);
        AssertFill(document, editor, "Group.Child", childLocked);
        Assert.Throws<PdfCertificationLockedException>(() => editor.Metadata.Title = "Nope");
        editor.Sign(new MemoryStream(), new PdfSignatureOptions { ReservedContentsBytes = 32 }, new FixedProvider());
    }

    [Fact]
    public void thousand_page_form_does_not_read_content_holes()
    {
        var builder = new PdfBuilder();
        var kids = new StringBuilder();
        for (int page = 0; page < 1000; page++)
        {
            if (page > 0)
            {
                kids.Append(' ');
            }

            kids.Append(page + 3).Append(" 0 R");
        }

        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [1003 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1000 /Kids [" + kids.ToString() + "] /MediaBox [0 0 500 600] >>");
        for (int page = 0; page < 1000; page++)
        {
            int id = page + 3;
            int contents = page + 1004;
            string rotate = page == 999 ? " /Rotate 90" : string.Empty;
            string annots = page == 999 ? " /Annots [1003 0 R]" : string.Empty;
            builder.Indirect(id, "<< /Type /Page /Parent 2 0 R /Contents " + contents.ToString() + " 0 R" + rotate + annots + " >>");
        }

        builder.Indirect(1003, "<< /Subtype /Widget /FT /Tx /T (Late) /Rect [10 20 30 40] /P 1002 0 R >>");
        long hole = builder.Length + 100;
        for (int page = 0; page < 1000; page++)
        {
            builder.Hole(page + 1004, hole);
        }

        (byte[] head, byte[] tail) = builder.Split(builder.Length + 2000000, string.Empty);
        var sparse = new SparsePdfStream(head, tail, head.Length + 2000000);
        using var document = PdfDocument.Open(sparse, leaveOpen: true);

        Assert.Equal(1000, document.PageCount);
        Assert.Equal(1000, document.Pages.Count);
        Assert.Equal(90, document.Pages[999].Rotation);
        Assert.Equal(500, document.Pages[999].MediaBox.Width);
        Assert.Equal(document.Pages[999].MediaBox, document.Pages[999].CropBox);
        PdfField field = Assert.Single(document.AcroForm.Fields);
        Assert.Equal("Late", field.FullName);
        Assert.Equal(1000, field.PageNumber);
        Assert.Equal(90, field.PageRotation);
        Assert.Equal(500, field.MediaBox.Width);
        Assert.Equal(600, field.MediaBox.Height);
        Assert.Equal(20, field.Rect.Width);
        Assert.False(sparse.TouchedHole);
    }

    [Fact]
    public void fills_reject_the_wrong_field_type_without_writing()
    {
        byte[] pdf = Form(
            "<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 10 10] /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Btn /T (Agree) /AP << /N << /Off null /Yes null >> >> /Rect [0 0 10 10] /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Btn /Ff 32768 /T (Color) /V /Red /AP << /N << /Off null /Red null >> >> /Rect [0 0 10 10] /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Ch /T (Choice) /Opt [(Red)] /Rect [0 0 10 10] /P 3 0 R >>");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Agree", "x"));
        Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Agree", PdfFieldValue.None));
        Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Agree", new[] { "Yes" }));
        Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Color", true));
        Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Color", new[] { "Red" }));
        Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Choice", true));
        Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Name", true));
        Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Name", PdfFieldValue.None));
        Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Name", new[] { "Yes" }));
        editor.SaveIncremental();

        Assert.Equal(pdf.Length, stream.Length);
    }

    [Fact]
    public void choice_options_reject_unknown_values_and_multi_select_is_an_array()
    {
        byte[] pdf = Form(
            "<< /Subtype /Widget /FT /Ch /T (Choice) /Ff 2 /Opt [(Red) (Blue)] /V (Red) /Rect [0 0 10 10] /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Ch /T (Pair) /Opt [[(a) (Alpha)] [(b) (Beta)]] /Rect [0 0 10 10] /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Ch /T (Editable) /Ff 262144 /Opt [(A)] /Rect [0 0 10 10] /P 3 0 R >>",
            "<< /Subtype /Widget /FT /Ch /T (Multi) /Ff 2097152 /Opt [(A) (B) (C)] /V [(A) (C)] /Rect [0 0 10 10] /P 3 0 R >>");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            PdfField choice = Field(document, "Choice");
            Assert.True(choice.Required);
            Assert.False(choice.Editable);
            Assert.Equal(new PdfChoiceOption("Red", "Red"), choice.Options[0]);
            Assert.Equal(new PdfChoiceOption("Blue", "Blue"), choice.Options[1]);
            Assert.Equal("Red", choice.Value);
            PdfField pair = Field(document, "Pair");
            Assert.Equal("a", pair.Options[0].Value);
            Assert.Equal("Alpha", pair.Options[0].Display);
            Assert.True(Field(document, "Editable").Editable);
            PdfField multi = Field(document, "Multi");
            Assert.True(multi.MultiSelect);
            Assert.Equal(new[] { "A", "C" }, multi.SelectedValues);

            long length = stream.Length;
            Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Choice", "No"));
            Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Pair", "Alpha"));
            Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Choice", new[] { "Red", "Blue" }));
            editor.SaveIncremental();
            Assert.Equal(length, stream.Length);

            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("Choice", "Blue"));
            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("Pair", "b"));
            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("Editable", "custom"));
            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("Multi", "B"));
            editor.SaveIncremental();
        }

        byte[] saved = stream.ToArray();
        string appendix = Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.Contains("[(B)]", appendix, StringComparison.Ordinal);
        Assert.Contains("(Beta)", appendix, StringComparison.Ordinal);
        using var again = PdfDocument.Open(stream, leaveOpen: true);
        Assert.Equal("Blue", Field(again, "Choice").Value);
        Assert.Equal("b", Field(again, "Pair").Value);
        Assert.Equal("custom", Field(again, "Editable").Value);
        Assert.Equal(new[] { "B" }, Field(again, "Multi").SelectedValues);
    }

    // LibreOffice copies /FT onto the widget kids of a field (corpus libreoffice-form). The value still belongs to the
    // field dictionary (ISO 32000-1 12.7.3.1), so a fill writes the parent's /V, and a stray /V on a widget kid is not read.
    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void widget_kids_with_field_entries_do_not_own_the_value()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 7 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> /Annots [5 0 R 6 0 R 8 0 R 9 0 R] >>");
        builder.Indirect(4, "<< /FT /Btn /Ff 49152 /T (Service) /V /FTL /Kids [5 0 R 6 0 R] >>");
        builder.Indirect(5, "<< /Type /Annot /Subtype /Widget /FT /Btn /Parent 4 0 R /P 3 0 R /Rect [10 10 20 20] /AP << /N << /FTL 10 0 R /Off 11 0 R >> >> /AS /FTL >>");
        builder.Indirect(6, "<< /Type /Annot /Subtype /Widget /FT /Btn /Parent 4 0 R /P 3 0 R /Rect [30 10 40 20] /V /LTL /AP << /N << /LTL 10 0 R /Off 11 0 R >> >> /AS /Off >>");
        builder.Indirect(7, "<< /FT /Tx /T (Name) /V (Old) /Kids [8 0 R 9 0 R] >>");
        builder.Indirect(8, "<< /Type /Annot /Subtype /Widget /FT /Tx /Parent 7 0 R /P 3 0 R /Rect [10 40 200 60] >>");
        builder.Indirect(9, "<< /Type /Annot /Subtype /Widget /FT /Tx /Parent 7 0 R /P 3 0 R /Rect [10 70 200 90] /V (Stray) >>");
        builder.StreamObject(10, "/Type /XObject /Subtype /Form /BBox [0 0 10 10]", Encoding.ASCII.GetBytes("0 g 2 2 6 6 re f"));
        builder.StreamObject(11, "/Type /XObject /Subtype /Form /BBox [0 0 10 10]", Array.Empty<byte>());
        byte[] pdf = builder.Finish();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.Equal("FTL", document.AcroForm.GetFields("Service")[1].Value);
            Assert.Equal(new[] { "Old", "Old" }, new[] { document.AcroForm.GetFields("Name")[0].Value, document.AcroForm.GetFields("Name")[1].Value });
            editor.AcroForm.Fill(new Dictionary<string, PdfFieldValue> { ["Service"] = "LTL", ["Name"] = "Ada" });
            editor.SaveIncremental();
        }

        byte[] saved = stream.ToArray();
        string appendix = Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.Contains("4 0 obj", appendix, StringComparison.Ordinal);
        Assert.Contains("7 0 obj", appendix, StringComparison.Ordinal);
        using var again = PdfDocument.Open(new MemoryStream(saved, writable: false), leaveOpen: true);
        Assert.Equal("LTL", again.AcroForm.Export()["Service"].Text);
        Assert.Equal("Ada", again.AcroForm.Export()["Name"].Text);
        Assert.True(again.AcroForm.GetFields("Service")[1].IsChecked);
        FormValues.AssertAgree(again.AcroForm.Export(), FormValues.Pypdf(saved), "LibreOffice-shaped kids");
    }

    [Fact]
    public void list_box_fill_removes_stale_indices_and_draws_the_selected_value()
    {
        byte[] pdf = Form("<< /Subtype /Widget /FT /Ch /T (List) /Opt [(A) (B) (C)] /V (A) /I [0] /Rect [0 0 100 60] /P 3 0 R >>");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("List", "B"));
            editor.SaveIncremental(stream);
        }

        string appendix = Encoding.ASCII.GetString(stream.ToArray(), pdf.Length, (int)(stream.Length - pdf.Length));
        Assert.DoesNotContain("/I [", appendix, StringComparison.Ordinal);
        Assert.Contains("(B) Tj", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("(A) Tj", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("(C) Tj", appendix, StringComparison.Ordinal);
    }

    [Fact]
    public void choice_without_options_does_not_dirty_the_document()
    {
        byte[] pdf = Form("<< /Subtype /Widget /FT /Ch /T (Choice) /Rect [0 0 10 10] /P 3 0 R >>");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        Assert.Empty(Field(document, "Choice").Options);
        Assert.False(Field(document, "Choice").Editable);
        Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Choice", "Yes"));
        editor.SaveIncremental(stream);

        Assert.Equal(pdf.Length, stream.Length);
    }

    [Fact]
    public void crop_box_inherits_and_defaults_to_the_media_box()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 3 /Kids [3 0 R 4 0 R 5 0 R] /MediaBox [0 0 500 600] /CropBox [1 2 301 402] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R >>");
        builder.Indirect(4, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /CropBox [9 9 19 29] >>");
        builder.Indirect(5, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 50 60] >>");
        byte[] pdf = builder.Finish();

        using var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true);

        Assert.Equal(new PdfRect(0, 0, 500, 600), document.Pages[0].MediaBox);
        Assert.Equal(new PdfRect(1, 2, 301, 402), document.Pages[0].CropBox);
        Assert.Equal(new PdfRect(0, 0, 200, 200), document.Pages[1].MediaBox);
        Assert.Equal(new PdfRect(9, 9, 19, 29), document.Pages[1].CropBox);
        Assert.Equal(new PdfRect(0, 0, 50, 60), document.Pages[2].MediaBox);
        Assert.Equal(new PdfRect(1, 2, 301, 402), document.Pages[2].CropBox);
    }

    [Fact]
    public void right_aligned_w_starts_left_of_i()
    {
        byte[] pdf = Form("<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 200 24] /Q 2 /DA (/Helv 12 Tf 0 g) /P 3 0 R >>");

        double wide = TmX(AppendixAfterFill(pdf, "Name", "W"));
        double narrow = TmX(AppendixAfterFill(pdf, "Name", "i"));

        Assert.True(wide < narrow);
    }

    [Fact]
    public void auto_sized_single_line_text_fits_width_down_to_the_six_point_floor()
    {
        const string value = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        byte[] fit = Form("<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 140 24] /DA (/Helv 0 Tf 0 g) /P 3 0 R >>");
        byte[] floor = Form("<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 40 24] /DA (/Helv 0 Tf 0 g) /P 3 0 R >>");

        double fittedSize = TfSize(AppendixAfterFill(fit, "Name", value));
        double floorSize = TfSize(AppendixAfterFill(floor, "Name", value));

        Assert.InRange(fittedSize, 6.001, 13.999);
        Assert.Equal(6, floorSize);
    }

    [Theory]
    [InlineData("[0.5]", "[0.25]", "0.5 g", "0.25 G")]
    [InlineData("[1 0 0]", "[0 0 1]", "1 0 0 rg", "0 0 1 RG")]
    [InlineData("[0 1 1 0]", "[1 0 0 0]", "0 1 1 0 k", "1 0 0 0 K")]
    public void text_fill_paints_supported_mk_colors_and_a_solid_border_inside_the_box(
        string background,
        string border,
        string backgroundOperator,
        string borderOperator)
    {
        byte[] pdf = Form("<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 100 24] /DA (/Helv 12 Tf 0 g) /MK << /BG " + background + " /BC " + border + " >> /BS << /W 4 /S /S >> /P 3 0 R >>");

        string appendix = AppendixAfterFill(pdf, "Name", "Ada");
        int backgroundIndex = appendix.IndexOf(backgroundOperator + "\n0 0 100 24 re f", StringComparison.Ordinal);
        int borderIndex = appendix.IndexOf(borderOperator + "\n4 w\n2 2 96 20 re S", StringComparison.Ordinal);
        int textIndex = appendix.IndexOf("BT\n/Helv 12 Tf", StringComparison.Ordinal);

        Assert.True(backgroundIndex >= 0);
        Assert.True(borderIndex > backgroundIndex);
        Assert.True(textIndex > borderIndex);
    }

    [Fact]
    public void text_fill_without_mk_keeps_the_existing_appearance_stream_shape()
    {
        byte[] pdf = Form("<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 100 24] /DA (/Helv 12 Tf 0 g) /P 3 0 R >>");

        string appendix = AppendixAfterFill(pdf, "Name", "Ada");

        Assert.Contains("q\nBT\n/Helv 12 Tf\n0 0 0 rg\n1 0 0 1 2 10 Tm\n(Ada) Tj\nET\nQ", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain(" re f\n", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain(" re S\n", appendix, StringComparison.Ordinal);
    }

    [Fact]
    public void choice_fill_paints_widget_mk_colors()
    {
        byte[] pdf = Form("<< /Subtype /Widget /FT /Ch /T (Choice) /Rect [0 0 100 24] /DA (/Helv 12 Tf 0 g) /Opt [(Ada)] /MK << /BG [1 0 0] /BC [0 0 1] >> /BS << /W 2 /S /S >> /P 3 0 R >>");

        string appendix = AppendixAfterFill(pdf, "Choice", "Ada");

        Assert.Contains("1 0 0 rg\n0 0 100 24 re f\n0 0 1 RG\n2 w\n1 1 98 22 re S", appendix, StringComparison.Ordinal);
    }

    [Fact]
    public void text_fill_preserves_the_widget_mk_dictionary()
    {
        byte[] pdf = Form("<< /Subtype /Widget /FT /Tx /T (Name) /Rect [0 0 100 24] /DA (/Helv 12 Tf 0 g) /MK << /BG [1 0 0] /BC [0 0 1] >> /P 3 0 R >>");

        string appendix = AppendixAfterFill(pdf, "Name", "Ada");

        Assert.Contains("/MK <<", appendix, StringComparison.Ordinal);
        Assert.Contains("/BG [1 0 0]", appendix, StringComparison.Ordinal);
        Assert.Contains("/BC [0 0 1]", appendix, StringComparison.Ordinal);
    }

    [Fact]
    public void multiline_www_wraps_sooner_than_iii()
    {
        byte[] pdf = Form("<< /Subtype /Widget /FT /Tx /T (Notes) /Rect [0 0 24 80] /Ff 4096 /DA (/Helv 12 Tf 0 g) /P 3 0 R >>");

        int wide = LineCount(AppendixAfterFill(pdf, "Notes", "WWW"));
        int narrow = LineCount(AppendixAfterFill(pdf, "Notes", "iii"));

        Assert.Equal(1, narrow);
        Assert.True(wide > narrow);
    }

    [Fact]
    public void kids_inherit_default_appearance()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R] >>");
        builder.Indirect(4, "<< /FT /Tx /T (Person) /DA (/Helv 12 Tf 0 0 1 rg) /Kids [5 0 R] >>");
        builder.Indirect(5, "<< /Subtype /Widget /Rect [10 20 110 40] >>");
        byte[] pdf = builder.Finish();

        string appendix = AppendixAfterFill(pdf, "Person", "Ada");

        Assert.Contains("/Helv 12 Tf\n0 0 1 rg\n", appendix, StringComparison.Ordinal);
    }

    [Fact]
    public void bare_check_box_appearance_is_named_yes_and_off()
    {
        byte[] pdf = Form("<< /Subtype /Widget /FT /Btn /T (Bare) /Rect [72 400 90 418] /P 3 0 R >>");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("Bare", true));
            editor.SaveIncremental(stream);
        }

        byte[] saved = stream.ToArray();
        string appendix = Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
        Assert.Contains("/N <<\n/Yes ", appendix, StringComparison.Ordinal);
        Assert.Contains("\n/Off ", appendix, StringComparison.Ordinal);
        using var again = PdfDocument.Open(new MemoryStream(saved), leaveOpen: true);
        PdfField bare = Field(again, "Bare");
        Assert.Equal("Yes", bare.ExportValue);
        Assert.Equal("Yes", bare.Value);
        Assert.True(bare.IsChecked);
    }

    [Fact]
    public void check_box_widgets_with_different_on_states_are_rejected_without_writing()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R 6 0 R] >>");
        builder.Indirect(4, "<< /FT /Btn /T (Agree) /Kids [5 0 R 6 0 R] >>");
        builder.Indirect(5, "<< /Subtype /Widget /Parent 4 0 R /Rect [10 10 30 30] /AP << /N << /Off null /First null >> >> /P 3 0 R >>");
        builder.Indirect(6, "<< /Subtype /Widget /Parent 4 0 R /Rect [40 10 60 30] /AP << /N << /Off null /Second null >> >> /P 3 0 R >>");
        byte[] pdf = builder.Finish();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        using var editor = document.Edit();

        PdfFieldValueException error = Assert.Throws<PdfFieldValueException>(() => editor.AcroForm.Fill("Agree", true));
        Assert.Contains("different on-state", error.Message, StringComparison.Ordinal);
        editor.SaveIncremental(stream);

        Assert.Equal(pdf.Length, stream.Length);
    }

    [Fact]
    public void radio_mark_is_cubic_curves_without_arc()
    {
        byte[] pdf = Form("<< /Subtype /Widget /FT /Btn /Ff 32768 /T (Color) /Rect [0 0 18 18] /P 3 0 R >>");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill("Color", "Red"));
            editor.SaveIncremental(stream);
        }

        string appendix = Encoding.ASCII.GetString(stream.ToArray(), pdf.Length, (int)(stream.Length - pdf.Length));
        Assert.Contains(" c\n", appendix, StringComparison.Ordinal);
        Assert.DoesNotContain("arc", appendix, StringComparison.Ordinal);
    }

    private static byte[] FieldMdpForm(string actionAndFields)
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 6 0 R 5 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 6 0 R 5 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Locked) /Rect [0 0 100 20] /P 3 0 R >>");
        builder.Indirect(6, "<< /Subtype /Widget /FT /Tx /T (Open) /Rect [0 30 100 50] /P 3 0 R >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Sig /T (Sig) /V 9 0 R /Rect [0 0 10 10] /P 3 0 R >>");
        builder.Indirect(9, "<< /Type /Sig /Reference [10 0 R] >>");
        builder.Indirect(10, "<< /TransformMethod /FieldMDP /TransformParams << /Action " + actionAndFields + " /V /1.2 >> >>");
        return builder.Finish();
    }

    private static byte[] NestedFieldMdpForm(string actionAndFields, bool certified)
    {
        var builder = new PdfBuilder();
        string perms = certified ? " /Perms << /DocMDP 9 0 R >>" : string.Empty;
        string docMdp = certified ? " 11 0 R" : string.Empty;
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R" + perms + " /AcroForm << /Fields [4 0 R 6 0 R 7 0 R 5 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 6 0 R 8 0 R 5 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Locked) /Rect [0 0 100 20] /P 3 0 R >>");
        builder.Indirect(6, "<< /Subtype /Widget /FT /Tx /T (Open) /Rect [0 30 100 50] /P 3 0 R >>");
        builder.Indirect(7, "<< /T (Group) /Kids [8 0 R] >>");
        builder.Indirect(8, "<< /Subtype /Widget /FT /Tx /T (Child) /Parent 7 0 R /Rect [0 60 100 80] /P 3 0 R >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Sig /T (Sig) /V 9 0 R /Rect [0 0 10 10] /P 3 0 R >>");
        builder.Indirect(9, "<< /Type /Sig /Reference [10 0 R" + docMdp + "] >>");
        builder.Indirect(10, "<< /TransformMethod /FieldMDP /TransformParams << /Action " + actionAndFields + " /V /1.2 >> >>");
        if (certified)
        {
            builder.Indirect(11, "<< /TransformMethod /DocMDP /TransformParams << /P 2 /V /1.2 >> >>");
        }

        return builder.Finish();
    }

    private static void AssertFill(PdfDocument document, PdfDocumentEditor editor, string fullName, bool locked)
    {
        if (locked)
        {
            Assert.Throws<PdfCertificationLockedException>(() => editor.AcroForm.Fill(fullName, "Ada"));
            Assert.Null(document.AcroForm.GetFields(fullName)[0].Value);
        }
        else
        {
            editor.AcroForm.Fill(fullName, "Ada");
            Assert.Equal("Ada", document.AcroForm.GetFields(fullName)[0].Value);
        }
    }

    private static byte[] Form(params string[] widgets)
    {
        return Build(widgets, string.Empty);
    }

    private static byte[] Build(string[] widgets, string catalogPerms)
    {
        var fields = new StringBuilder();
        var annots = new StringBuilder();
        for (int i = 0; i < widgets.Length; i++)
        {
            if (i > 0)
            {
                fields.Append(' ');
                annots.Append(' ');
            }

            int id = i + 4;
            fields.Append(id).Append(" 0 R");
            annots.Append(id).Append(" 0 R");
        }

        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R" + catalogPerms + " /AcroForm << /Fields [" + fields.ToString() + "] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [" + annots.ToString() + "] >>");
        for (int i = 0; i < widgets.Length; i++)
        {
            builder.Indirect(i + 4, widgets[i]);
        }

        return builder.Finish();
    }

    private static PdfField Field(PdfDocument document, string name)
    {
        return Assert.Single(document.AcroForm.GetFields(name));
    }

    private static int IndexOf(PdfDocument document, string name, int widget)
    {
        for (int i = 0; i < document.AcroForm.Fields.Count; i++)
        {
            PdfField field = document.AcroForm.Fields[i];
            if (field.FullName == name && field.WidgetIndex == widget)
            {
                return i;
            }
        }

        throw new InvalidOperationException(name);
    }

    private static string AppendixAfterFill(byte[] pdf, string name, string value)
    {
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            using var editor = document.Edit();
            Assert.Equal(PdfAppearanceDisposition.Generated, editor.AcroForm.Fill(name, value));
            editor.SaveIncremental(stream);
        }

        byte[] saved = stream.ToArray();
        return Encoding.ASCII.GetString(saved, pdf.Length, saved.Length - pdf.Length);
    }

    private static double TmX(string appendix)
    {
        const string marker = "1 0 0 1 ";
        int start = appendix.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        start += marker.Length;
        int end = appendix.IndexOf(' ', start);
        return double.Parse(appendix.Substring(start, end - start), CultureInfo.InvariantCulture);
    }

    private static double TfSize(string appendix)
    {
        const string marker = "/Helv ";
        int end = appendix.LastIndexOf(" Tf", StringComparison.Ordinal);
        Assert.True(end > 0);
        int start = appendix.LastIndexOf(marker, end, StringComparison.Ordinal);
        Assert.True(start >= 0);
        start += marker.Length;
        Assert.True(end > start);
        return double.Parse(appendix.Substring(start, end - start), CultureInfo.InvariantCulture);
    }

    private static int LineCount(string appendix)
    {
        int count = 0;
        int index = 0;
        while ((index = appendix.IndexOf(" Tj", index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += 3;
        }

        return count;
    }

    private sealed class FixedProvider : IPdfSignatureProvider
    {
        public PdfSignatureKind Kind => PdfSignatureKind.CmsDetached;

        public ValueTask<byte[]> CreateContentsAsync(PdfSignatureContext context, CancellationToken cancellationToken)
        {
            return new ValueTask<byte[]>(new byte[] { 1, 2, 3, 4 });
        }
    }
}
