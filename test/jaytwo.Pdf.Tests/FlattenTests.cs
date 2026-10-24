using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace jaytwo.Pdf.Tests;

// Spec W3, 7.1, and 7.2 (W-77). Structure is read back by qpdf --check and by pypdf (Fixtures/form_values.py for the
// field tree, Fixtures/flatten_probe.py for the page /Annots and /Contents). Rendering is in AppearanceRenderTests.
public class FlattenTests
{
    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void flatten_paints_each_shown_appearance_and_removes_the_widgets_and_fields()
    {
        byte[] pdf = BuildForm(string.Empty, string.Empty);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            FillForm(document);
            document.AcroForm.Flatten();
            Assert.False(document.AcroForm.HasFields);
            Assert.Empty(document.AcroForm.Fields);
            Assert.False(document.AcroForm.NeedAppearances);
            document.SaveIncremental();
        }

        byte[] saved = stream.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        string appendix = Appendix(saved, pdf.Length);

        // Name, Agree, Station, and Group.Child are drawn. Hidden has a value but is hidden, Empty shows no value and has
        // no appearance, and Nowhere is in no page's /Annots.
        Assert.Equal(4, Regex.Matches(appendix, @" cm /FlatAP\d+ Do Q").Count);
        Assert.Matches(@"/Contents \[\d+ 0 R 20 0 R \d+ 0 R\]", appendix);
        Assert.Contains("q\n", appendix, StringComparison.Ordinal);
        Assert.Contains("\nQ\nq ", appendix, StringComparison.Ordinal);
        Assert.Matches(@"/Font <<\s*/F1 21 0 R", appendix);
        Assert.DoesNotContain("NeedAppearances", appendix, StringComparison.Ordinal);
        Assert.Matches(@"/CO \[\]", appendix);

        FormValues.AssertQpdfCheck(saved);
        Assert.Empty(FormValues.Pypdf(saved));
        JsonElement probe = Probe(saved);
        JsonElement page = probe.GetProperty("pages")[0];
        Assert.Equal(0, page.GetProperty("widgets").GetArrayLength());
        Assert.Equal(3, page.GetProperty("contents").GetInt32());
        Assert.Equal(0, probe.GetProperty("fields").GetInt32());
        Assert.DoesNotContain("/NeedAppearances", probe.GetProperty("acroform").EnumerateArray().Select(key => key.GetString()));
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void named_flatten_keeps_the_other_fields_and_removes_an_emptied_parent()
    {
        byte[] pdf = BuildForm(string.Empty, string.Empty);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            FillForm(document);
            document.AcroForm.Flatten(new[] { "Group.Child", "Agree" });
            string[] remaining = document.AcroForm.Fields.Select(field => field.FullName).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToArray();
            Assert.Equal(new[] { "Empty", "Hidden", "Name", "Nowhere", "Station" }, remaining);
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Name", "Grace"));
            document.SaveIncremental();
        }

        byte[] saved = stream.ToArray();
        string appendix = Appendix(saved, pdf.Length);
        Assert.Equal(2, Regex.Matches(appendix, @" cm /FlatAP\d+ Do Q").Count);
        Assert.Matches(@"/Fields \[4 0 R 6 0 R 10 0 R 11 0 R 12 0 R\]", appendix);

        FormValues.AssertQpdfCheck(saved);
        Dictionary<string, JsonElement> fields = FormValues.Pypdf(saved);
        Assert.Equal(new[] { "Empty", "Hidden", "Name", "Nowhere", "Station" }, fields.Keys.OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal("Grace", fields["Name"].GetProperty("value").GetString());
        JsonElement widgets = Probe(saved).GetProperty("pages")[0].GetProperty("widgets");
        Assert.Equal(new[] { "Empty", "Hidden", "Name", "Station" }, widgets.EnumerateArray().Select(item => item.GetString()).OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void appearance_placement_follows_the_matrix_and_bbox_algorithm()
    {
        // ISO 32000-1 12.5.5: /BBox [10 20 110 40] under /Matrix [0 1 -1 0 0 0] spans x -40 to -20 and y 10 to 110. Mapped
        // onto /Rect [100 100 120 200] that is a unit scale and a translation by (140, 90). The second widget's box is
        // twice its rectangle, so it scales by one half.
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 5 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Turned) /V (x) /Rect [120 200 100 100] /AP << /N 6 0 R >> /P 3 0 R >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Tx /T (Half) /V (x) /Rect [300 300 350 320] /AP << /N 7 0 R >> /P 3 0 R >>");
        builder.StreamObject(6, "/Type /XObject /Subtype /Form /BBox [10 20 110 40] /Matrix [0 1 -1 0 0 0]", Encoding.ASCII.GetBytes("0 g 10 20 100 20 re f"));
        builder.StreamObject(7, "/Subtype /Form /BBox [0 0 100 40]", Encoding.ASCII.GetBytes("0 g 0 0 100 40 re f"));
        byte[] pdf = builder.Finish();

        string appendix = FlattenAppendix(pdf, document => document.AcroForm.Flatten());

        Assert.Contains("q 1 0 0 1 140 90 cm /FlatAP0 Do Q", appendix, StringComparison.Ordinal);
        Assert.Contains("q 0.5 0 0 0.5 300 300 cm /FlatAP1 Do Q", appendix, StringComparison.Ordinal);
    }

    [Fact]
    public void appearance_without_a_subtype_gains_subtype_form_so_do_paints_it()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Bare) /V (x) /Rect [10 10 110 30] /AP << /N 5 0 R >> /P 3 0 R >>");
        builder.StreamObject(5, "/BBox [0 0 100 20]", Encoding.ASCII.GetBytes("0 g 0 0 100 20 re f"));
        byte[] pdf = builder.Finish();

        string appendix = FlattenAppendix(pdf, document => document.AcroForm.Flatten());

        Assert.Matches(@"5 0 obj\n<<\n/BBox \[0 0 100 20\]\n/Length 19\n/Subtype /Form\n>>\nstream\n0 g 0 0 100 20 re f\nendstream", appendix);
    }

    [Fact]
    [Trait(Lane.Trait, Lane.Interop)]
    public void indirect_annots_and_contents_and_inherited_resources_are_kept_and_names_do_not_collide()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm 9 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] /Resources << /XObject << /FlatAP0 7 0 R >> /Font << /F1 8 0 R >> >> >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots 10 0 R /Contents 11 0 R >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /V (x) /Rect [10 10 110 30] /AP << /N 5 0 R >> /P 3 0 R >>");
        builder.StreamObject(5, "/Subtype /Form /BBox [0 0 100 20]", Encoding.ASCII.GetBytes("0 g 0 0 100 20 re f"));
        builder.StreamObject(7, "/Subtype /Form /BBox [0 0 1 1]", Encoding.ASCII.GetBytes("0 g"));
        builder.Indirect(8, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        builder.Indirect(9, "<< /Fields 13 0 R >>");
        builder.Indirect(10, "[4 0 R 14 0 R]");
        builder.Indirect(11, "[12 0 R 12 0 R]");
        builder.StreamObject(12, string.Empty, Encoding.ASCII.GetBytes("0 g"));
        builder.Indirect(13, "[4 0 R]");
        builder.Indirect(14, "<< /Subtype /Link /Rect [0 0 1 1] >>");
        byte[] pdf = builder.Finish();

        string appendix = FlattenAppendix(pdf, document => document.AcroForm.Flatten(), out byte[] saved);

        Assert.Matches(@"10 0 obj\n\[14 0 R\]", appendix);
        Assert.Matches(@"13 0 obj\n\[\]", appendix);
        Assert.DoesNotMatch(@"\n9 0 obj", appendix);
        Assert.Matches(@"/Contents \[\d+ 0 R 12 0 R 12 0 R \d+ 0 R\]", appendix);
        Assert.Matches(@"/FlatAP0 7 0 R\n/FlatAP1 5 0 R", appendix);
        Assert.Matches(@"/F1 8 0 R", appendix);
        Assert.Contains("cm /FlatAP1 Do Q", appendix, StringComparison.Ordinal);

        FormValues.AssertQpdfCheck(saved);
        JsonElement page = Probe(saved).GetProperty("pages")[0];
        Assert.Equal(0, page.GetProperty("widgets").GetArrayLength());
        Assert.Equal(1, page.GetProperty("other").GetInt32());
        Assert.Equal(4, page.GetProperty("contents").GetInt32());
    }

    [Fact]
    public void optional_content_widget_is_painted_inside_its_optional_content()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> /OCProperties << /OCGs [6 0 R] /D << /OFF [6 0 R] >> >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Layered) /V (x) /OC 6 0 R /Rect [10 10 110 30] /AP << /N 5 0 R >> /P 3 0 R >>");
        builder.StreamObject(5, "/Subtype /Form /BBox [0 0 100 20]", Encoding.ASCII.GetBytes("0 g 0 0 100 20 re f"));
        builder.Indirect(6, "<< /Type /OCG /Name (Layer) >>");
        byte[] pdf = builder.Finish();

        string appendix = FlattenAppendix(pdf, document => document.AcroForm.Flatten());

        Assert.Contains("/OC /FlatOC1 BDC\nq 1 0 0 1 10 10 cm /FlatAP0 Do Q\nEMC\n", appendix, StringComparison.Ordinal);
        Assert.Matches(@"/Properties <<\s*/FlatOC1 6 0 R", appendix);
    }

    [Fact]
    public void check_box_states_select_the_appearance_and_off_without_an_appearance_draws_nothing()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R 6 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 5 0 R 6 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Btn /T (On) /V /Yes /AS /Yes /Rect [10 10 30 30] /AP << /N << /Yes 7 0 R /Off 8 0 R >> >> /P 3 0 R >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Btn /T (Off) /V /Off /AS /Off /Rect [40 10 60 30] /AP << /N << /Yes 7 0 R >> >> /P 3 0 R >>");
        builder.Indirect(6, "<< /Subtype /Widget /FT /Btn /T (Lone) /V /Yes /Rect [70 10 90 30] /AP << /N << /Yes 7 0 R >> >> /P 3 0 R >>");
        builder.StreamObject(7, "/Subtype /Form /BBox [0 0 20 20]", Encoding.ASCII.GetBytes("0 g 2 2 16 16 re f"));
        builder.StreamObject(8, "/Subtype /Form /BBox [0 0 20 20]", Encoding.ASCII.GetBytes("0.5 g 2 2 16 16 re S"));
        byte[] pdf = builder.Finish();

        string appendix = FlattenAppendix(pdf, document => document.AcroForm.Flatten());

        // On and Lone (no /AS, one state) paint /Yes; Off has no /Off appearance and paints nothing.
        Assert.Contains("q 1 0 0 1 10 10 cm /FlatAP0 Do Q\nq 1 0 0 1 70 10 cm /FlatAP0 Do Q\n", appendix, StringComparison.Ordinal);
        Assert.Matches(@"/FlatAP0 7 0 R\n>>", appendix);
    }

    [Fact]
    public void hidden_noview_and_empty_widgets_are_removed_without_being_drawn()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R 6 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 9 0 R /Annots [4 0 R 5 0 R 6 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Hidden) /F 2 /V (x) /Rect [10 10 110 30] /AP << /N 7 0 R >> /P 3 0 R >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Tx /T (NoView) /F 32 /V (x) /Rect [10 40 110 60] /AP << /N 7 0 R >> /P 3 0 R >>");
        builder.Indirect(6, "<< /Subtype /Widget /FT /Tx /T (Empty) /Rect [10 70 110 90] /P 3 0 R >>");
        builder.StreamObject(7, "/Subtype /Form /BBox [0 0 100 20]", Encoding.ASCII.GetBytes("0 g 0 0 100 20 re f"));
        builder.StreamObject(9, string.Empty, Encoding.ASCII.GetBytes("0 g"));
        byte[] pdf = builder.Finish();

        string appendix = FlattenAppendix(pdf, document => document.AcroForm.Flatten());

        Assert.DoesNotContain(" Do ", appendix, StringComparison.Ordinal);
        Assert.Contains("/Contents 9 0 R", appendix, StringComparison.Ordinal);
        Assert.Matches(@"/Annots \[\]", appendix);
        Assert.Matches(@"/Fields \[\]", appendix);
    }

    [Fact]
    public void signature_fields_are_kept_by_a_full_flatten_and_an_unsigned_one_goes_when_named()
    {
        byte[] pdf = BuildForm(string.Empty, string.Empty, signatureField: true);
        using (var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true))
        {
            FillForm(document);
            document.AcroForm.Flatten();
            Assert.Equal("Sig", Assert.Single(document.AcroForm.Fields).FullName);
            document.AcroForm.Flatten(new[] { "Sig" });
            Assert.Empty(document.AcroForm.Fields);
        }
    }

    [Fact]
    public void argument_and_name_errors_change_nothing()
    {
        byte[] pdf = BuildForm(string.Empty, string.Empty);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);

        Assert.Throws<ArgumentNullException>(() => document.AcroForm.Flatten(null!));
        Assert.Throws<ArgumentException>(() => document.AcroForm.Flatten(new[] { "Name", null! }));
        Assert.Throws<ArgumentException>(() => document.AcroForm.Flatten(new[] { "Name", "Name" }));
        Assert.Throws<PdfFieldNotFoundException>(() => document.AcroForm.Flatten(new[] { "Name", "Missing" }));
        document.AcroForm.Flatten(Array.Empty<string>());
        AssertUnchanged(document, stream, pdf);
    }

    [Fact]
    public void a_value_without_an_appearance_and_need_appearances_refuse_before_any_change()
    {
        byte[] pdf = BuildForm(string.Empty, string.Empty);
        using (var document = PdfDocument.Open(new MemoryStream(pdf), leaveOpen: true))
        {
            Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Name", "Ada"));
            Assert.Equal(PdfAppearanceDisposition.NeedsViewer, document.AcroForm.Fill("Group.Child", "中"));
            Assert.True(document.AcroForm.NeedAppearances);
            Assert.Throws<PdfUnsupportedFormException>(() => document.AcroForm.Flatten(new[] { "Name" }));
            Assert.Equal(7, document.AcroForm.Fields.Select(field => field.FullName).Distinct().Count());
        }

        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R] /NeedAppearances false >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 5 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Bare) /V (Ada) /Rect [10 10 110 30] /P 3 0 R >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Btn /T (Box) /V /Yes /AS /Yes /Rect [10 40 30 60] /AP << /N << /Off 6 0 R >> >> /P 3 0 R >>");
        builder.StreamObject(6, "/Subtype /Form /BBox [0 0 20 20]", Encoding.ASCII.GetBytes("0 g"));
        byte[] bare = builder.Finish();
        using var bareStream = new MemoryStream();
        bareStream.Write(bare, 0, bare.Length);
        using var bareDocument = PdfDocument.Open(bareStream, leaveOpen: true);
        Assert.Throws<PdfUnsupportedFormException>(() => bareDocument.AcroForm.Flatten(new[] { "Bare" }));
        Assert.Throws<PdfUnsupportedFormException>(() => bareDocument.AcroForm.Flatten(new[] { "Box" }));
        AssertUnchanged(bareDocument, bareStream, bare);
    }

    // An appearance without a /BBox, or that is an image, is not a form XObject (ISO 32000-1 Table 95), and viewers draw
    // nothing for it: an empty field loses nothing and paints nothing, and a field with a value refuses.
    [Fact]
    public void an_unusable_appearance_paints_nothing_for_an_empty_field_and_refuses_a_value()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R 6 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 5 0 R 6 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (NoBox) /Rect [10 10 110 30] /AP << /N 7 0 R >> /P 3 0 R >>");
        builder.Indirect(5, "<< /Subtype /Widget /FT /Tx /T (Image) /Rect [10 40 110 60] /AP << /N 8 0 R >> /P 3 0 R >>");
        builder.Indirect(6, "<< /Subtype /Widget /FT /Tx /T (Valued) /V (Ada) /Rect [10 70 110 90] /AP << /N 7 0 R >> /P 3 0 R >>");
        builder.StreamObject(7, "/Subtype /Form", Encoding.ASCII.GetBytes("0 g 0 0 100 20 re f"));
        builder.StreamObject(8, "/Subtype /Image /BBox [0 0 100 20] /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8", new byte[] { 0 });
        byte[] pdf = builder.Finish();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);

        Assert.Throws<PdfUnsupportedFormException>(() => document.AcroForm.Flatten());
        AssertUnchanged(document, stream, pdf);
        document.AcroForm.Flatten(new[] { "NoBox", "Image" });
        Assert.Equal("Valued", Assert.Single(document.AcroForm.Fields).FullName);
        document.SaveIncremental();
        Assert.DoesNotContain(" Do ", Appendix(stream.ToArray(), pdf.Length), StringComparison.Ordinal);
    }

    [Fact]
    public void no_rotate_widget_on_a_rotated_page_refuses()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] /Rotate 90 >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R] >>");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Fixed) /F 20 /V (x) /Rect [10 10 110 30] /AP << /N 5 0 R >> /P 3 0 R >>");
        builder.StreamObject(5, "/Subtype /Form /BBox [0 0 100 20]", Encoding.ASCII.GetBytes("0 g"));
        byte[] pdf = builder.Finish();
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);

        Assert.Throws<PdfUnsupportedFormException>(() => document.AcroForm.Flatten());
        AssertUnchanged(document, stream, pdf);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void docmdp_blocks_flatten_at_every_level_even_with_the_opt_in(int permission)
    {
        byte[] pdf = BuildForm(" /Perms << /DocMDP 30 0 R >>", "<< /Type /Sig /Reference [<< /TransformMethod /DocMDP /TransformParams << /P " + permission + " /V /1.2 >> >>] >>");
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);
        document.AllowBreakingSignatures = true;

        Assert.Throws<PdfCertificationLockedException>(() => document.AcroForm.Flatten());
        Assert.Throws<PdfCertificationLockedException>(() => document.AcroForm.Flatten(new[] { "Name" }));
        AssertUnchanged(document, stream, pdf);
    }

    [Fact]
    public void a_signature_requires_the_opt_in_and_fieldmdp_and_signed_fields_refuse()
    {
        byte[] pdf = BuildForm(string.Empty, "<< /Type /Sig /Reference [<< /TransformMethod /FieldMDP /TransformParams << /Action /Include /Fields [(Name)] /V /1.2 >> >>] >>", signatureField: true);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using var document = PdfDocument.Open(stream, leaveOpen: true);

        Assert.Throws<PdfSignatureConflictException>(() => document.AcroForm.Flatten(new[] { "Agree" }));
        document.AllowBreakingSignatures = true;
        Assert.Throws<PdfCertificationLockedException>(() => document.AcroForm.Flatten(new[] { "Name" }));
        Assert.Throws<PdfCertificationLockedException>(() => document.AcroForm.Flatten());
        Assert.Throws<PdfSignatureConflictException>(() => document.AcroForm.Flatten(new[] { "Sig" }));
        AssertUnchanged(document, stream, pdf);

        Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill("Agree", true));
        document.AcroForm.Flatten(new[] { "Agree" });
        Assert.DoesNotContain(document.AcroForm.Fields, field => field.FullName == "Agree");
        Assert.Contains(document.AcroForm.Fields, field => field.FullName == "Sig");
    }

    [Fact]
    public void xfa_only_refuses_and_a_hybrid_flatten_removes_xfa()
    {
        byte[] xfaOnly = BuildForm(" /NeedsRendering true", string.Empty, xfa: true);
        using (var stream = new MemoryStream())
        {
            stream.Write(xfaOnly, 0, xfaOnly.Length);
            using var document = PdfDocument.Open(stream, leaveOpen: true);
            Assert.True(document.AcroForm.IsXfaOnly);
            Assert.Throws<PdfUnsupportedFormException>(() => document.AcroForm.Flatten());
            AssertUnchanged(document, stream, xfaOnly);
        }

        byte[] hybrid = BuildForm(string.Empty, string.Empty, xfa: true);
        using (var document = PdfDocument.Open(new MemoryStream(hybrid), leaveOpen: true))
        {
            Assert.True(document.AcroForm.IsHybridXfa);
            document.AcroForm.Flatten(new[] { "Empty" });
            Assert.False(document.AcroForm.IsHybridXfa);
            Assert.Contains(document.AcroForm.Fields, field => field.FullName == "Name");
        }

        // A signed hybrid form needs the opt-in like any signed document, and the flatten then removes /XFA too.
        byte[] signed = BuildForm(string.Empty, "<< /Type /Sig >>", signatureField: true, xfa: true);
        using (var stream = new MemoryStream())
        {
            stream.Write(signed, 0, signed.Length);
            using var document = PdfDocument.Open(stream, leaveOpen: true);
            Assert.Throws<PdfSignatureConflictException>(() => document.AcroForm.Flatten(new[] { "Empty" }));
            AssertUnchanged(document, stream, signed);
            document.AllowBreakingSignatures = true;
            document.AcroForm.Flatten(new[] { "Empty" });
            Assert.False(document.AcroForm.IsHybridXfa);
        }
    }

    [Fact]
    public void usage_rights_are_removed_on_an_unsigned_document_and_kept_on_a_signed_one()
    {
        const string ur3 = " /Perms << /UR3 31 0 R >>";
        const string rights = "<< /Type /Sig /Reference [<< /TransformMethod /UR3 /TransformParams << /Form [/FillIn /Delete] /V /2.2 >> >>] >>";
        byte[] unsigned = BuildForm(ur3, string.Empty, usageRights: rights);
        using (var document = PdfDocument.Open(new MemoryStream(unsigned), leaveOpen: true))
        {
            Assert.True(document.HasUsageRights);
            document.AcroForm.Flatten(new[] { "Empty" });
            Assert.False(document.HasUsageRights);
        }

        byte[] signed = BuildForm(ur3, "<< /Type /Sig >>", signatureField: true, usageRights: rights);
        using (var document = PdfDocument.Open(new MemoryStream(signed), leaveOpen: true))
        {
            document.AllowBreakingSignatures = true;
            document.AcroForm.Flatten(new[] { "Empty" });
            Assert.True(document.HasUsageRights);
        }
    }

    // Plan C4: qpdf encrypts the form; the fill and the flatten are encrypted with the document's cipher under the owner
    // and the user password, /Encrypt and /ID are kept, and qpdf and pypdf read the result. A user password without the
    // modify or the annotate bit refuses and changes nothing.
    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData("256", "owner")]
    [InlineData("256", "user")]
    [InlineData("128-rc4", "owner")]
    [InlineData("128-rc4", "user")]
    public void encrypted_flatten_keeps_the_cipher_and_reads_back_in_qpdf_and_pypdf(string cipher, string password)
    {
        byte[] pdf = QpdfEncrypt(BuildForm(string.Empty, string.Empty), cipher);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, password, leaveOpen: true))
        {
            FillForm(document);
            document.AcroForm.Flatten();
            document.SaveIncremental();
        }

        byte[] saved = stream.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        string appendix = Appendix(saved, pdf.Length);
        Assert.DoesNotContain(" Do Q", appendix, StringComparison.Ordinal);
        Assert.Contains("/Encrypt ", appendix, StringComparison.Ordinal);
        Assert.Contains("/ID ", appendix, StringComparison.Ordinal);
        FormValues.AssertQpdfCheck(saved, password);
        Assert.Empty(FormValues.Pypdf(saved, password));
        Assert.Equal(0, Probe(saved, password).GetProperty("pages")[0].GetProperty("widgets").GetArrayLength());
        using var reopened = PdfDocument.Open(new MemoryStream(saved), password);
        Assert.False(reopened.AcroForm.HasFields);
    }

    [Theory]
    [Trait(Lane.Trait, Lane.External)]
    [InlineData("--modify-other=n")]
    [InlineData("--annotate=n")]
    public void user_password_without_modify_or_annotate_refuses(string restriction)
    {
        byte[] pdf = QpdfEncrypt(BuildForm(string.Empty, string.Empty), "256", restriction);
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, "user", leaveOpen: true))
        {
            Assert.Throws<PdfEncryptionRestrictedException>(() => document.AcroForm.Flatten());
            AssertUnchanged(document, stream, pdf);
        }

        using (var document = PdfDocument.Open(stream, "owner", leaveOpen: true))
        {
            document.AcroForm.Flatten();
            Assert.False(document.AcroForm.HasFields);
        }
    }

    // Spec G7: flattening a field on the last of 1,000 pages reads that page's content stream dictionary, to tell a
    // stream from an array, and nothing of the other pages' content, which sits in the hole.
    [Fact]
    public void thousand_page_flatten_does_not_read_content_holes()
    {
        var builder = new PdfBuilder();
        var kids = new StringBuilder();
        for (int page = 0; page < 1000; page++)
        {
            kids.Append(page == 0 ? string.Empty : " ").Append(page + 3).Append(" 0 R");
        }

        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [1003 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1000 /Kids [" + kids.ToString() + "] /MediaBox [0 0 500 600] >>");
        for (int page = 0; page < 1000; page++)
        {
            string annots = page == 999 ? " /Annots [1003 0 R]" : string.Empty;
            builder.Indirect(page + 3, "<< /Type /Page /Parent 2 0 R /Contents " + (page + 1004).ToString(System.Globalization.CultureInfo.InvariantCulture) + " 0 R" + annots + " >>");
        }

        builder.Indirect(1003, "<< /Subtype /Widget /FT /Tx /T (Late) /V (x) /Rect [10 20 30 40] /AP << /N 2004 0 R >> /P 1002 0 R >>");
        builder.StreamObject(2004, "/Subtype /Form /BBox [0 0 20 20]", Encoding.ASCII.GetBytes("0 g"));
        builder.StreamObject(2003, string.Empty, Encoding.ASCII.GetBytes("0 g"));
        long hole = builder.Length + 100;
        for (int page = 0; page < 999; page++)
        {
            builder.Hole(page + 1004, hole);
        }

        (byte[] head, byte[] tail) = builder.Split(builder.Length + 2000000, string.Empty);
        var sparse = new SparsePdfStream(head, tail, head.Length + 2000000);
        using var document = PdfDocument.Open(sparse, leaveOpen: true);

        document.AcroForm.Flatten();
        Assert.False(document.AcroForm.HasFields);
        document.SaveIncremental();
        Assert.False(sparse.TouchedHole);
    }

    // One page. Name, Agree (check box), Station (a radio with one widget kid), Group.Child, Hidden (hidden, with a value),
    // Empty (no value, no appearance), and Nowhere (in no /Annots). /CO lists Name. With `signatureField`, Sig is a
    // signature field whose /V is object 30 when `signature` is set. With `xfa`, the AcroForm has an /XFA array.
    private static byte[] BuildForm(string catalogExtra, string signature, bool signatureField = false, bool xfa = false, string? usageRights = null)
    {
        string sig = signatureField ? " 13 0 R" : string.Empty;
        string xfaEntry = xfa ? " /XFA [(template) 24 0 R]" : string.Empty;
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R 6 0 R 8 0 R 10 0 R 11 0 R 12 0 R" + sig + "] /CO [4 0 R] /NeedAppearances false /DA (/Helv 0 Tf 0 g)" + xfaEntry + " >>" + catalogExtra + " >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 20 0 R /Resources << /Font << /F1 21 0 R >> >> /Annots [4 0 R 5 0 R 7 0 R 9 0 R 10 0 R 11 0 R" + sig + "] >>");
        builder.Indirect(4, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /Rect [72 700 280 724] /DA (/Helv 12 Tf 0 g) /F 4 /P 3 0 R >>");
        builder.Indirect(5, "<< /Type /Annot /Subtype /Widget /FT /Btn /T (Agree) /Rect [72 660 90 678] /F 4 /P 3 0 R >>");
        builder.Indirect(6, "<< /FT /Btn /Ff 49152 /T (Station) /Kids [7 0 R] >>");
        builder.Indirect(7, "<< /Type /Annot /Subtype /Widget /Parent 6 0 R /Rect [72 620 90 638] /F 4 /P 3 0 R >>");
        builder.Indirect(8, "<< /T (Group) /Kids [9 0 R] >>");
        builder.Indirect(9, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Child) /Parent 8 0 R /Rect [72 580 280 604] /DA (/Helv 12 Tf 0 g) /F 4 /P 3 0 R >>");
        builder.Indirect(10, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Hidden) /V (Secret) /Rect [300 700 500 724] /F 2 /P 3 0 R >>");
        builder.Indirect(11, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Empty) /Rect [300 660 500 684] /DA (/Helv 12 Tf 0 g) /F 4 /P 3 0 R >>");
        builder.Indirect(12, "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Nowhere) /Rect [300 620 500 644] /DA (/Helv 12 Tf 0 g) /F 4 >>");
        string value = signature.Length > 0 ? " /V 30 0 R" : string.Empty;
        builder.Indirect(13, "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Sig) /Rect [300 40 500 80] /F 4 /P 3 0 R" + value + " >>");
        builder.StreamObject(20, string.Empty, Encoding.ASCII.GetBytes("BT /F1 18 Tf 72 750 Td (Header) Tj ET"));
        builder.Indirect(21, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        builder.StreamObject(24, string.Empty, Encoding.ASCII.GetBytes("<template xmlns=\"http://www.xfa.org/schema/xfa-template/2.5/\"/>"));
        if (signature.Length > 0)
        {
            builder.Indirect(30, signature);
        }

        if (usageRights != null)
        {
            builder.Indirect(31, usageRights);
        }

        return builder.Finish();
    }

    private static void FillForm(PdfDocument document)
    {
        Assert.Equal(PdfAppearanceDisposition.Generated, document.AcroForm.Fill(new Dictionary<string, PdfFieldValue>
        {
            ["Name"] = "Ada Lovelace",
            ["Agree"] = true,
            ["Station"] = "Yes",
            ["Group.Child"] = "Kid",
        }).Values.Distinct().Single());
    }

    private static string FlattenAppendix(byte[] pdf, Action<PdfDocument> flatten)
    {
        return FlattenAppendix(pdf, flatten, out _);
    }

    private static string FlattenAppendix(byte[] pdf, Action<PdfDocument> flatten, out byte[] saved)
    {
        using var stream = new MemoryStream();
        stream.Write(pdf, 0, pdf.Length);
        using (var document = PdfDocument.Open(stream, leaveOpen: true))
        {
            flatten(document);
            document.SaveIncremental();
        }

        saved = stream.ToArray();
        Assert.True(saved.AsSpan(0, pdf.Length).SequenceEqual(pdf));
        return Appendix(saved, pdf.Length);
    }

    private static string Appendix(byte[] saved, int prefix)
    {
        return Encoding.ASCII.GetString(saved, prefix, saved.Length - prefix);
    }

    // A refused flatten leaves the document clean, so a save writes nothing.
    private static void AssertUnchanged(PdfDocument document, MemoryStream stream, byte[] pdf)
    {
        document.SaveIncremental();
        Assert.Equal(pdf.Length, stream.Length);
    }

    private static JsonElement Probe(byte[] pdf, string? password = null)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "flattened.pdf");
            File.WriteAllBytes(path, pdf);
            var args = new List<string> { ExternalTool.Fixture("flatten_probe.py"), path };
            if (password != null)
            {
                args.Add(password);
            }

            using Process process = ExternalTool.Start(ExternalTool.Python(), args.ToArray());
            using JsonDocument document = JsonDocument.Parse(ExternalTool.Output(process));
            return document.RootElement.Clone();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // qpdf's 128-bit RC4 is revision 3 and its AES-256 revision 6. User password "user", owner password "owner".
    private static byte[] QpdfEncrypt(byte[] pdf, string cipher, string? restriction = null)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string input = Path.Combine(directory, "plain.pdf");
            string output = Path.Combine(directory, "encrypted.pdf");
            File.WriteAllBytes(input, pdf);
            var args = new List<string> { "--allow-weak-crypto", "--encrypt", "user", "owner" };
            args.AddRange(cipher == "128-rc4" ? new[] { "128", "--use-aes=n" } : new[] { "256" });
            if (restriction != null)
            {
                args.Add(restriction);
            }

            args.AddRange(new[] { "--", input, output });
            using Process process = ExternalTool.Start(CorpusManifest.Qpdf(), args.ToArray());
            (int exitCode, string stdout, string stderr) = ExternalTool.Run(process);
            Assert.True(exitCode == 0, stderr + stdout);
            return File.ReadAllBytes(output);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
