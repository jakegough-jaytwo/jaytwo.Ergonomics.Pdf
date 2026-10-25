using System;
using System.IO;
using System.Linq;
using System.Text;
using jaytwo.Pdf.Internal;
using Xunit;

namespace jaytwo.Pdf.Tests;

// Original MIT-licensed focused fixtures, not qualification-corpus members.
public sealed class PageConcatenationTests
{
    [Fact]
    public void repeated_occurrences_keep_fields_values_and_links_separate_and_widgets_page_owned()
    {
        byte[] bytes = Form();
        using PdfDocument document = PdfDocument.Open(new MemoryStream(bytes));
        using var output = new MemoryStream();
        var inputs = new[]
        {
            new PdfAssemblyInput(document, new[] { 1, 1 }),
            new PdfAssemblyInput(document, new[] { 1 }),
        };
        PdfDocumentEditor.Concatenate(inputs, output);
        using PdfDocument combined = PdfDocument.Open(new MemoryStream(output.ToArray()));
        Assert.Equal(3, combined.PageCount);
        Assert.Equal(new[] { "Input1.Name", "Input1.Name", "Input2.Name" }, combined.AcroForm.Fields.Select(field => field.FullName));
        using var store = Store(output.ToArray());
        int[] ids = combined.Pages.Select(page => page.ObjectId).ToArray();
        for (int i = 0; i < ids.Length; i++)
        {
            var page = (PdfObject.Dict)store.Resolve(new PdfObject.Ref(ids[i], 0));
            var annotations = (PdfObject.Arr)store.Resolve(page.Get("Annots")!);
            Assert.Equal(2, annotations.Items.Count);
            var widget = (PdfObject.Dict)store.Resolve(annotations.Items[0]);
            Assert.Equal(ids[i], ((PdfObject.Ref)widget.Get("P")!).ObjectId);
            Assert.Null(widget.Get("T"));
            var link = (PdfObject.Dict)store.Resolve(annotations.Items[1]);
            var dest = (PdfObject.Arr)link.Get("Dest")!;
            Assert.Equal(ids[i], ((PdfObject.Ref)dest.Items[0]).ObjectId);
        }

        using PdfDocumentEditor editor = combined.Edit();
        editor.AcroForm.Fill("Input1.Name", "Ada");
        using var saved = new MemoryStream();
        editor.SaveIncremental(saved);
        using PdfDocument reread = PdfDocument.Open(new MemoryStream(saved.ToArray()));
        Assert.Equal("Ada", reread.AcroForm.GetFields("Input1.Name")[0].Value!.ToString());
        Assert.Equal("Original", reread.AcroForm.GetFields("Input2.Name")[0].Value!.ToString());
    }

    [Fact]
    public void links_to_other_pages_stay_in_their_input_occurrence()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count 2 /Kids [3 0 R 4 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Annots [<< /Subtype /Link /Dest [4 0 R /Fit] >>] >>");
        builder.Indirect(4, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>");
        using PdfDocument document = PdfDocument.Open(new MemoryStream(builder.Finish()));
        using var output = new MemoryStream();
        var inputs = new[] { new PdfAssemblyInput(document, new[] { 1, 2 }), new PdfAssemblyInput(document, new[] { 2, 1 }) };
        PdfDocumentEditor.Concatenate(inputs, output);
        using PdfDocument combined = PdfDocument.Open(new MemoryStream(output.ToArray()));
        using var store = Store(output.ToArray());
        int[] ids = combined.Pages.Select(page => page.ObjectId).ToArray();
        foreach (int index in new[] { 0, 3 })
        {
            var page = (PdfObject.Dict)store.Resolve(new PdfObject.Ref(ids[index], 0));
            var annotations = (PdfObject.Arr)page.Get("Annots")!;
            var link = (PdfObject.Dict)store.Resolve(annotations.Items[0]);
            var destination = (PdfObject.Arr)link.Get("Dest")!;
            Assert.Equal(ids[index == 0 ? 1 : 2], ((PdfObject.Ref)destination.Items[0]).ObjectId);
        }
    }

    [Theory]
    [InlineData("/AA << /K << /S /JavaScript /JS (evil) >> >>", "")]
    [InlineData("", "/CO [4 0 R]")]
    [InlineData("", "/DR << /XObject << /X 5 0 R >> >>")]
    [InlineData("/DA (/Missing 12 Tf)", "")]
    public void unpreservable_forms_in_a_later_input_fail_before_output(string field, string form)
    {
        using PdfDocument first = PdfDocument.Open(new MemoryStream(PdfBuilder.Basic().Finish()));
        using PdfDocument second = PdfDocument.Open(new MemoryStream(Form(field, form)));
        using var output = new MemoryStream();
        var inputs = new[] { new PdfAssemblyInput(first, new[] { 1 }), new PdfAssemblyInput(second, new[] { 1 }) };
        Assert.Throws<PdfAssemblyNotSupportedException>(() => PdfDocumentEditor.Concatenate(inputs, output));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void a_later_invalid_selection_and_disposed_input_are_checked_before_output()
    {
        using PdfDocument document = PdfDocument.Open(new MemoryStream(PdfBuilder.Basic().Finish()));
        using var output = new MemoryStream();
        var inputs = new[] { new PdfAssemblyInput(document, new[] { 1 }), new PdfAssemblyInput(document, new[] { 2 }) };
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfDocumentEditor.Concatenate(inputs, output));
        Assert.Equal(0, output.Length);
        document.Dispose();
        Assert.Throws<ObjectDisposedException>(() => PdfDocumentEditor.Concatenate(new[] { new PdfAssemblyInput(document, new[] { 1 }) }, output));
        Assert.Equal(0, output.Length);
    }

    [Theory]
    [InlineData(2, 4, 5)]
    [InlineData(3, 5, 11)]
    [InlineData(4, 5, 11)]
    [InlineData(6, 5, 11)]
    public void encrypted_permissions_require_both_bits_on_every_user_input_and_owner_bypasses(int revision, int first, int second)
    {
        int required = (1 << (first - 1)) | (1 << (second - 1));
        byte[] bytes = Encrypted(revision, -4 & ~required);
        using PdfDocument owner = PdfDocument.Open(new MemoryStream(bytes), "owner");
        using PdfDocument user = PdfDocument.Open(new MemoryStream(bytes), "user");
        using var output = new MemoryStream();
        var options = new PdfAssemblyOptions { AllowUnencryptedOutput = true };
        Assert.Throws<PdfAssemblyNotSupportedException>(() => PdfDocumentEditor.Concatenate(new[] { new PdfAssemblyInput(owner, new[] { 1 }) }, output));
        Assert.Equal(0, output.Length);
        var inputs = new[] { new PdfAssemblyInput(owner, new[] { 1 }), new PdfAssemblyInput(user, new[] { 1 }) };
        Assert.Throws<PdfEncryptionRestrictedException>(() => PdfDocumentEditor.Concatenate(inputs, output, options));
        Assert.Equal(0, output.Length);
        PdfDocumentEditor.Concatenate(new[] { new PdfAssemblyInput(owner, new[] { 1 }) }, output, options);
        Assert.DoesNotContain("/Encrypt", Encoding.ASCII.GetString(output.ToArray()));
        foreach (int missing in new[] { first, second })
        {
            using PdfDocument restricted = PdfDocument.Open(new MemoryStream(Encrypted(revision, -4 & ~(1 << (missing - 1)))), "user");
            using var refused = new MemoryStream();
            Assert.Throws<PdfEncryptionRestrictedException>(() => PdfDocumentEditor.Concatenate(new[] { new PdfAssemblyInput(restricted, new[] { 1 }) }, refused, options));
            Assert.Equal(0, refused.Length);
        }

        // Bits for printing, accessibility, and (for R3+) modification are irrelevant to assembly policy.
        using PdfDocument allowed = PdfDocument.Open(new MemoryStream(Encrypted(revision, required | unchecked((int)0xfffff0c0))), "user");
        using var accepted = new MemoryStream();
        PdfDocumentEditor.Concatenate(new[] { new PdfAssemblyInput(allowed, new[] { 1 }) }, accepted, options);
        using var store = Store(accepted.ToArray());
        var pages = (PdfObject.Dict)store.Resolve(store.CatalogDict().Get("Pages")!);
        var page = (PdfObject.Dict)store.Resolve(((PdfObject.Arr)pages.Get("Kids")!).Items[0]);
        var content = (PdfObject.Dict)store.Resolve(page.Get("Contents")!);
        Assert.Equal(new byte[] { 0x61, 0x62, 0x63, 0x80 }, Read(store.OpenRawBody(content)));
        Assert.Equal("RunLengthDecode", ((PdfObject.Name)content.Get("Filter")!).Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void explicit_crypt_filters_are_removed_with_only_their_parameters(bool identity)
    {
        using PdfDocument document = PdfDocument.Open(new MemoryStream(Encrypted(4, -4, identity ? "Identity" : "StdCF")), "user");
        using PdfDocumentEditor editor = document.Edit();
        using var output = new MemoryStream();
        editor.ExtractPages(new[] { 1 }, output, new PdfAssemblyOptions { AllowUnencryptedOutput = true });
        using var store = Store(output.ToArray());
        var pages = (PdfObject.Dict)store.Resolve(store.CatalogDict().Get("Pages")!);
        var page = (PdfObject.Dict)store.Resolve(((PdfObject.Arr)pages.Get("Kids")!).Items[0]);
        var content = (PdfObject.Dict)store.Resolve(page.Get("Contents")!);
        Assert.Equal(new byte[] { 0x61, 0x62, 0x63, 0x80 }, Read(store.OpenRawBody(content)));
        var filters = (PdfObject.Arr)content.Get("Filter")!;
        Assert.Single(filters.Items);
        Assert.Equal("RunLengthDecode", ((PdfObject.Name)filters.Items[0]).Value);
        var parameters = (PdfObject.Arr)content.Get("DecodeParms")!;
        Assert.Single(parameters.Items);
        Assert.Equal(7, ((PdfObject.Num)((PdfObject.Dict)parameters.Items[0]).Get("Columns")!).LongValue);
        Assert.DoesNotContain("/Crypt", Encoding.ASCII.GetString(output.ToArray()));
        Assert.DoesNotContain("/StdCF", Encoding.ASCII.GetString(output.ToArray()));
    }

    [Fact]
    public void nested_fields_prune_unselected_widgets_keep_inheritance_and_repeated_widget_copies()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [6 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 2 /Kids [3 0 R 4 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Annots [8 0 R] >>");
        builder.Indirect(4, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Annots [9 0 R] >>");
        builder.Indirect(6, "<< /T (Group) /FT /Tx /V (inherited) /Kids [7 0 R] >>");
        builder.Indirect(7, "<< /T (Name) /Parent 6 0 R /Kids [8 0 R 9 0 R] >>");
        builder.Indirect(8, "<< /Subtype /Widget /Parent 7 0 R /FT /Tx /Rect [0 0 20 20] >>");
        builder.Indirect(9, "<< /Subtype /Widget /Parent 7 0 R /Rect [0 0 20 20] >>");
        using PdfDocument document = PdfDocument.Open(new MemoryStream(builder.Finish()));
        using var output = new MemoryStream();
        PdfDocumentEditor.Concatenate(new[] { new PdfAssemblyInput(document, new[] { 1, 1 }) }, output);
        using PdfDocument combined = PdfDocument.Open(new MemoryStream(output.ToArray()));
        Assert.Equal(2, combined.AcroForm.Fields.Count);
        Assert.All(combined.AcroForm.Fields, field =>
        {
            Assert.Equal("Input1.Group.Name", field.FullName);
            Assert.Equal("inherited", field.Value!.ToString());
        });
    }

    [Theory]
    [InlineData("RC4-128")]
    [InlineData("AES-128")]
    [InlineData("AES-256")]
    [Trait(Lane.Trait, Lane.Interop)]
    public void foreign_forms_and_encrypted_streams_survive_concatenation_and_both_renderers(string algorithm)
    {
        string directory = ExternalTool.TempDirectory();
        try
        {
            string source = Path.Combine(directory, "source.pdf");
            string encrypted = Path.Combine(directory, "encrypted.pdf");
            string output = Path.Combine(directory, "combined.pdf");
            // Original text and form content. pypdf (BSD-3-Clause) independently creates the source and encrypts it.
            const string script = "import sys\nfrom pypdf import PdfWriter\nfrom pypdf.generic import DictionaryObject as D,NameObject as N,ArrayObject as A,NumberObject as I,TextStringObject as T,DecodedStreamObject\nw=PdfWriter();p=w.add_blank_page(144,72)\nf=w._add_object(D({N('/Type'):N('/Font'),N('/Subtype'):N('/Type1'),N('/BaseFont'):N('/Helvetica')}))\ns=DecodedStreamObject();s.set_data(b'BT /F1 12 Tf 2 20 Td (Original) Tj ET');s.update({N('/Type'):N('/XObject'),N('/Subtype'):N('/Form'),N('/BBox'):A([I(n) for n in [0,0,120,30]]),N('/Resources'):D({N('/Font'):D({N('/F1'):f})})})\na=w._add_object(s.flate_encode());widget=w._add_object(D({N('/Type'):N('/Annot'),N('/Subtype'):N('/Widget'),N('/FT'):N('/Tx'),N('/T'):T('Name'),N('/V'):T('Original'),N('/Rect'):A([I(n) for n in [10,10,130,40]]),N('/AP'):D({N('/N'):a}),N('/P'):p.indirect_reference}))\np[N('/Annots')]=A([widget]);w._root_object[N('/AcroForm')]=D({N('/Fields'):A([widget]),N('/DA'):T('/F1 12 Tf 0 g'),N('/DR'):D({N('/Font'):D({N('/F1'):f})})})\nw.write(sys.argv[1]);w.encrypt('user','owner',algorithm=sys.argv[3]);w.write(sys.argv[2])\n";
            ExternalTool.Output(ExternalTool.Start(ExternalTool.Python(), "-c", script, source, encrypted, algorithm));
            using (PdfDocument first = PdfDocument.Open(File.OpenRead(source)))
            using (PdfDocument second = PdfDocument.Open(File.OpenRead(encrypted), "owner"))
            using (var destination = File.Create(output))
            {
                var inputs = new[] { new PdfAssemblyInput(first, new[] { 1 }), new PdfAssemblyInput(second, new[] { 1 }), new PdfAssemblyInput(first, new[] { 1 }) };
                PdfDocumentEditor.Concatenate(inputs, destination, new PdfAssemblyOptions { AllowUnencryptedOutput = true });
            }

            string qpdf = ExternalTool.Locate("JAYTWO_QPDF", ExternalTool.Executable("qpdf"));
            ExternalTool.Output(ExternalTool.Start(qpdf, "--check", output));
            const string inspect = "import sys\nfrom pypdf import PdfReader\nr=PdfReader(sys.argv[1]);assert not r.is_encrypted;assert len(r.pages)==3\nassert [n for n,v in r.get_fields().items() if '/FT' in v]==['Input1.Name','Input2.Name','Input3.Name']\nfor p in r.pages:\n a=p['/Annots'][0].get_object()['/AP']['/N'];assert a['/Filter']=='/FlateDecode';assert b'Original' in a.get_data()\n";
            ExternalTool.Output(ExternalTool.Start(ExternalTool.Python(), "-c", inspect, output));
            foreach (RenderTool renderer in RenderTool.All())
            {
                Assert.Equal(3, renderer.Text(output).Split(new[] { "Original" }, StringSplitOptions.None).Length - 1);
                renderer.RasterPages(directory, renderer.Name + "-source", source);
                renderer.RasterPages(directory, renderer.Name + "-combined", output);
                for (int page = 1; page <= 3; page++)
                {
                    Assert.Equal(renderer.ReadRaster(directory, renderer.Name + "-source", 1, 1), renderer.ReadRaster(directory, renderer.Name + "-combined", page, 3));
                }
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static byte[] Form(string field = "", string form = "")
    {
        var builder = PdfBuilder.Basic(
            " /AcroForm << /Fields [4 0 R] /DA (/F1 12 Tf 0 g) /DR << /Font << /F1 5 0 R >> >> " + form + " >>",
            " /Annots [4 0 R << /Subtype /Link /Rect [0 0 10 10] /Dest [3 0 R /Fit] >>]");
        builder.Indirect(4, "<< /Subtype /Widget /FT /Tx /T (Name) /V (Original) /Rect [10 10 100 30] /P 3 0 R " + field + " >>");
        builder.Indirect(5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        return builder.Finish();
    }

    private static byte[] Encrypted(int revision, int permissions, string? cryptFilter = null)
    {
        StandardEncryptor.Kind kind = revision switch
        {
            2 => StandardEncryptor.Kind.Revision2,
            3 => StandardEncryptor.Kind.Revision3,
            4 => StandardEncryptor.Kind.Revision4Aes,
            _ => StandardEncryptor.Kind.Revision6,
        };
        var crypt = new StandardEncryptor(kind, "user", "owner", permissions, true);
        var builder = PdfBuilder.Basic(pageExtra: " /Contents 4 0 R");
        byte[] encoded = { 0x61, 0x62, 0x63, 0x80 };
        string filters = cryptFilter == null ? "/Filter /RunLengthDecode"
            : "/Filter [/Crypt /RunLengthDecode] /DecodeParms [<< /Name /" + cryptFilter + " >> << /Columns 7 >>]";
        builder.StreamObject(4, filters, cryptFilter == "Identity" ? encoded : crypt.Encrypt(encoded, 4, 0));
        builder.Indirect(20, crypt.Dictionary(null));
        return builder.Finish("/Encrypt 20 0 R /ID [" + crypt.IdLiteral() + "]");
    }

    private static PdfObjectStore Store(byte[] bytes)
    {
        var store = new PdfObjectStore(new TestPdfSource(bytes), true, 1);
        new PdfDocumentLoader(store).InitializeAsync(null, false, default).GetAwaiter().GetResult();
        return store;
    }

    private static byte[] Read(Stream input)
    {
        using (input)
        using (var output = new MemoryStream())
        {
            input.CopyTo(output);
            return output.ToArray();
        }
    }
}
