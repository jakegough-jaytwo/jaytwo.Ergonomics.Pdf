using System;
using System.IO;
using System.Linq;
using System.Text;
using jaytwo.Pdf.Internal;
using Xunit;

namespace jaytwo.Pdf.Tests;

// Inline inputs are original MIT-licensed test data. Foreign fixture provenance lives beside the PDFs.
public sealed class AssemblyLinksTests
{
    [Fact]
    public void destinations_target_own_copy_or_first_other_copy_and_named_targets_become_direct()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /Dests << /old [4 0 R /Fit] >> /Names << /Dests << /Kids [9 0 R] >> >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 3 /Kids [3 0 R 4 0 R 5 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Annots [<< /Subtype /Link /Rect [0 0 10 10] /Dest [3 0 R /XYZ null 10 0] >> << /Subtype /Link /Dest /old >> << /Subtype /Link /A << /S /GoTo /D (new) >> >> << /Subtype /Link /Dest [5 0 R /Fit] >>] >>");
        builder.Indirect(4, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>");
        builder.Indirect(5, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>");
        builder.Indirect(9, "<< /Names [(new) << /D [4 0 R /FitH 20] >>] >>");
        var extracted = Extract(builder.Finish(), new[] { 1, 1, 2, 2 }, PdfExternalLinkMode.Drop);
        Assert.Equal(2, extracted.Result.Omissions[PdfOmissionReason.MissingDestination]);
        using var source = new MemoryStream(extracted.Bytes);
        using var store = new PdfObjectStore(new TestPdfSource(source.ToArray()), true, 1);
        new PdfDocumentLoader(store).InitializeAsync(null, false, default).GetAwaiter().GetResult();
        using PdfDocument document = PdfDocument.Open(new MemoryStream(extracted.Bytes));
        int[] ids = document.Pages.Select(page => page.ObjectId).ToArray();
        for (int index = 0; index < 2; index++)
        {
            var page = (PdfObject.Dict)store.Resolve(new PdfObject.Ref(ids[index], 0));
            var annotations = (PdfObject.Arr)page.Get("Annots")!;
            Assert.Equal(3, annotations.Items.Count);
            for (int link = 0; link < 3; link++)
            {
                var annotation = (PdfObject.Dict)store.Resolve(annotations.Items[link]);
                var destination = (PdfObject.Arr)annotation.Get("Dest")!;
                Assert.Equal(link == 0 ? ids[index] : ids[2], ((PdfObject.Ref)destination.Items[0]).ObjectId);
            }
        }

        Assert.DoesNotContain("/Names", Encoding.ASCII.GetString(extracted.Bytes));
    }

    [Theory]
    [InlineData("/S /URI /URI (https://example.org/) /Next null", "")]
    [InlineData("/S /URI /URI (https://example.org/) /IsMap true", "")]
    [InlineData("/S /URI /URI (https://example.org/)", "/AA null")]
    [InlineData("/S /URI /URI (https://user@example.org/)", "")]
    [InlineData("/S /URI /URI (file:///tmp/a)", "")]
    [InlineData("/S /URI /URI (relative)", "")]
    [InlineData("/S /URI /URI (https://example.org/%xx)", "")]
    [InlineData("/S /Launch /F (private-marker)", "")]
    [InlineData("/S /GoToR /D [0 /Fit]", "")]
    public void unsupported_links_are_omitted_in_their_entirety(string action, string extra)
    {
        byte[] input = PdfBuilder.Basic(pageExtra: " /Annots [<< /Subtype /Link /Rect [0 0 10 10] /Contents (private-marker) /A << " + action + " >> " + extra + " >>]").Finish();
        var output = Extract(input, new[] { 1 });
        Assert.Equal(1, output.Result.Omissions[PdfOmissionReason.UnsupportedLink]);
        Assert.DoesNotContain("/Annots", Encoding.ASCII.GetString(output.Bytes));
        Assert.DoesNotContain("private-marker", Encoding.ASCII.GetString(output.Bytes));
    }

    [Theory]
    [InlineData("https://example.org/a/b", "../c?x=1#f", "https://example.org/c?x=1#f")]
    [InlineData("https://example.org/a/b/", "../c", "https://example.org/a/c")]
    [InlineData("https://example.org/a", "//other.org/x", "https://other.org/x")]
    [InlineData("https://example.org/", "mailto:a@example.org", "mailto:a@example.org")]
    public void relative_uris_follow_rfc3986_and_drop_option_removes_external_links(string basis, string uri, string expected)
    {
        byte[] input = PdfBuilder.Basic(" /URI << /Base (" + basis + ") >>", " /Annots [<< /Subtype /Link /Rect [0 0 10 10] /PA (private-marker) /A << /S /URI /URI (" + uri + ") >> >>]").Finish();
        var kept = Extract(input, new[] { 1 });
        Assert.Empty(kept.Result.Omissions);
        string text = Encoding.ASCII.GetString(kept.Bytes);
        Assert.Contains(expected, text);
        Assert.DoesNotContain("private-marker", text);
        var dropped = Extract(input, new[] { 1 }, PdfExternalLinkMode.Drop);
        Assert.Equal(1, dropped.Result.Omissions[PdfOmissionReason.ExternalLinkDropped]);
        Assert.DoesNotContain("/Annots", Encoding.ASCII.GetString(dropped.Bytes));
    }

    [Theory]
    [InlineData("not-an-absolute-base")]
    [InlineData("https://user@example.org/a/")]
    [InlineData("file:///tmp/a/")]
    public void invalid_or_unsupported_catalog_bases_omit_relative_links(string basis)
    {
        byte[] input = PdfBuilder.Basic(" /URI << /Base (" + basis + ") >>", " /Annots [<< /Subtype /Link /A << /S /URI /URI (child) >> >>]").Finish();
        var output = Extract(input, new[] { 1 });
        Assert.Equal(1, output.Result.Omissions[PdfOmissionReason.UnsupportedLink]);
        Assert.DoesNotContain("/Annots", Encoding.ASCII.GetString(output.Bytes));
    }

    [Fact]
    public void appearances_are_preflighted_before_any_owned_object_is_registered()
    {
        var builder = PdfBuilder.Basic(pageExtra: " /Annots [<< /Subtype /Text /AP << /N 4 0 R >> >> << /Subtype /Text /Contents (omitted-marker) /AP << /N 5 0 R >> >>]");
        builder.StreamObject(4, "/Type /XObject /Subtype /Form /BBox [0 0 10 10]", Encoding.ASCII.GetBytes("0 0 10 10 re S"));
        builder.StreamObject(5, "/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << /Font << /F1 6 0 R >> >>", new byte[PdfAssemblyAppearances.MaxBytes + 1]);
        builder.Indirect(6, "<< /Type /Font /Subtype /Type1 /BaseFont /omitted-font >>");
        var output = Extract(builder.Finish(), new[] { 1 });
        Assert.Equal(1, output.Result.Omissions[PdfOmissionReason.UnsupportedAppearance]);
        string text = Encoding.ASCII.GetString(output.Bytes);
        Assert.Contains("0 0 10 10 re S", text);
        Assert.DoesNotContain("omitted-marker", text);
        Assert.DoesNotContain("omitted-font", text);
        Assert.True(output.Bytes.Length < 2000);
    }

    [Fact]
    public void appearance_global_limits_and_source_integrity_failures_abort()
    {
        using var source = new MemoryStream(PdfBuilder.Basic().Finish());
        using var store = new PdfObjectStore(new TestPdfSource(source.ToArray()), true, 1);
        new PdfDocumentLoader(store).InitializeAsync(null, false, default).GetAwaiter().GetResult();
        var inspector = new PdfAssemblyAppearances(store, new PdfNewFileBudget(new PdfNewFileLimits(mappedObjects: 0)));
        // A malformed or unresolved source is an operation failure, never a local omission.
        Assert.Throws<PdfParseException>(() => inspector.Supported(new PdfObject.Ref(99, 0)));
        var appearance = new PdfObject.Dict();
        var form = new PdfObject.Dict();
        form.Set("Subtype", new PdfObject.Name("Form"));
        var box = new PdfObject.Arr(4);
        for (int index = 0; index < 4; index++)
        {
            box.Items.Add(PdfObject.Num.FromLong(0));
        }

        form.Set("BBox", box);
        form.Body = new PdfObject.MemoryBody(Array.Empty<byte>());
        appearance.Set("N", form);
        Assert.Throws<PdfResourceLimitException>(() => inspector.Supported(appearance));
    }

    [Theory]
    [InlineData("depth")]
    [InlineData("objects")]
    [InlineData("unknown")]
    public void local_appearance_limits_and_unclassifiable_resources_omit_the_annotation(string risk)
    {
        var builder = PdfBuilder.Basic(pageExtra: " /Annots [<< /Subtype /Text /Contents (owned-marker) /AP << /N 4 0 R >> >>]");
        string resources = risk == "unknown" ? " /UnknownObject (owned-marker)" : string.Empty;
        if (risk == "depth")
        {
            resources = " /Resources << /XObject << /Nested 5 0 R >> >>";
            for (int id = 5; id < 45; id++)
            {
                builder.Indirect(id, "<< /Resources " + (id + 1) + " 0 R >>");
            }

            builder.Indirect(45, "<< >>");
        }
        else if (risk == "objects")
        {
            resources = " /Resources << /Font <<";
            for (int id = 5; id < 5 + PdfAssemblyAppearances.MaxObjects; id++)
            {
                resources += " /F" + id + " " + id + " 0 R";
                builder.Indirect(id, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            }

            resources += " >> >>";
        }

        builder.StreamObject(4, "/Type /XObject /Subtype /Form /BBox [0 0 10 10]" + resources, Encoding.ASCII.GetBytes("owned-stream-marker"));
        var output = Extract(builder.Finish(), new[] { 1 });
        Assert.Equal(1, output.Result.Omissions[PdfOmissionReason.UnsupportedAppearance]);
        Assert.DoesNotContain("owned-", Encoding.ASCII.GetString(output.Bytes));
        Assert.DoesNotContain("/Annots", Encoding.ASCII.GetString(output.Bytes));
    }

    [Fact]
    public void unresolved_appearance_aborts_extraction_before_output()
    {
        using var source = new MemoryStream(PdfBuilder.Basic(pageExtra: " /Annots [<< /Subtype /Text /AP << /N 99 0 R >> >>]").Finish());
        using PdfDocument document = PdfDocument.Open(source);
        using PdfDocumentEditor editor = document.Edit();
        using var output = new MemoryStream();
        Assert.Throws<PdfParseException>(() => editor.ExtractPages(new[] { 1 }, output, PdfExternalLinkMode.PreserveSupported));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void omission_result_has_fixed_reason_cardinality()
    {
        var output = Extract(PdfBuilder.Basic(pageExtra: " /Annots [<< /Subtype /Movie >>]").Finish(), Enumerable.Repeat(1, 1000).ToArray());
        Assert.Single(output.Result.Omissions);
        Assert.Equal(1000, output.Result.Omissions[PdfOmissionReason.UnsupportedAnnotation]);
    }

    [Theory]
    [Trait(Lane.Trait, Lane.Interop)]
    [InlineData("list-of-uri-actions.pdf", 0, 2)]
    [InlineData("list-of-uri-actions-with-base.pdf", 1, 1)]
    [InlineData("uri-ismap-test.pdf", 0, 1)]
    [InlineData("LinkAnnot-appearances.pdf", 0, 1)]
    public void pdf_association_link_fixtures_follow_the_whitelist(string name, int retained, int omitted)
    {
        byte[] input = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "focused-assembly-links", name));
        var output = Extract(input, new[] { 1 });
        Assert.Equal(omitted, output.Result.Omissions.Values.Sum());
        string text = Encoding.ASCII.GetString(output.Bytes);
        Assert.Equal(retained, text.Split(new[] { "/Subtype /Link" }, StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("/Next", text);
        Assert.DoesNotContain("/IsMap", text);
        if (name == "list-of-uri-actions-with-base.pdf")
        {
            Assert.Contains("/AP", text);
            Assert.Contains("10 10 80 80 re B", text);
        }

        string directory = ExternalTool.TempDirectory();
        try
        {
            string path = Path.Combine(directory, "output.pdf");
            File.WriteAllBytes(path, output.Bytes);
            string qpdf = ExternalTool.Locate("JAYTWO_QPDF", ExternalTool.Executable("qpdf"));
            ExternalTool.Output(ExternalTool.Start(qpdf, "--check", path));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static (byte[] Bytes, PdfAssemblyResult Result) Extract(byte[] bytes, int[] pages, PdfExternalLinkMode mode = PdfExternalLinkMode.PreserveSupported)
    {
        using var source = new MemoryStream(bytes);
        using PdfDocument document = PdfDocument.Open(source);
        using PdfDocumentEditor editor = document.Edit();
        using var output = new MemoryStream();
        PdfAssemblyResult result = editor.ExtractPages(pages, output, mode);
        return (output.ToArray(), result);
    }
}
