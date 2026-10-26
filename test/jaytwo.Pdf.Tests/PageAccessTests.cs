using System.Linq;
using System.Text;
using jaytwo.Pdf.Internal;
using Xunit;

namespace jaytwo.Pdf.Tests;

// W-184: one editor session lists its pages once; repeated numbered lookups reuse the same handles.
public class PageAccessTests
{
    private const int PageTotal = 50;

    [Fact]
    public void repeated_page_lookups_walk_the_page_tree_once_and_keep_rotation_current()
    {
        using PdfDocument document = OpenCounted(ManyPages());
        PdfWorkCounters work = document.WorkCounters!;
        using PdfDocumentEditor editor = document.Edit();
        long visitsBefore = work.PageNodeVisits;

        PdfPageEditor first = editor.Pages[2];
        editor.Pages[2].Rotation += 90;
        editor.Pages[2].Rotation += 90;
        editor.Pages[PageTotal - 1].Rotation = 270;

        Assert.Equal(PageTotal + 1, work.PageNodeVisits - visitsBefore);
        Assert.Same(first, editor.Pages[2]);
        Assert.Equal(180, first.Rotation);
        Assert.Equal(180, document.Pages[2].Rotation);
        Assert.Equal(270, document.Pages[PageTotal - 1].Rotation);
        Assert.Equal(0, document.Pages[0].Rotation);
    }

    [Fact]
    public void all_page_rotation_then_a_numbered_rotation_matches_the_engine_result()
    {
        using PdfDocument document = OpenCounted(ManyPages());
        using PdfDocumentEditor editor = document.Edit();

        foreach (PdfPageEditor page in editor.Pages)
        {
            page.Rotation += 90;
        }

        editor.Pages[4].Rotation += 90;
        editor.Pages[4].Rotation -= 270;

        int[] rotations = document.Pages.Select(page => page.Rotation).ToArray();
        Assert.Equal(270, rotations[4]);
        Assert.All(rotations.Where((_, index) => index != 4), rotation => Assert.Equal(90, rotation));
    }

    [Fact]
    public void a_later_editor_sees_the_rotation_an_earlier_editor_left_pending()
    {
        using PdfDocument document = OpenCounted(ManyPages());
        using (PdfDocumentEditor first = document.Edit())
        {
            first.Pages[1].Rotation = 90;
        }

        using PdfDocumentEditor second = document.Edit();
        Assert.Equal(90, second.Pages[1].Rotation);
    }

    [Fact]
    public void page_access_stays_lazy_and_stops_with_the_document()
    {
        PdfDocument document = OpenCounted(ManyPages());
        PdfWorkCounters work = document.WorkCounters!;
        PdfDocumentEditor editor = document.Edit();
        long visitsBefore = work.PageNodeVisits;

        Assert.Equal(visitsBefore, work.PageNodeVisits);
        Assert.Equal(PageTotal, editor.Pages.Count);
        document.Dispose();

        Assert.Throws<System.ObjectDisposedException>(() => editor.Pages);
    }

    private static PdfDocument OpenCounted(byte[] pdf)
    {
        using (PdfWorkCounters.Collect())
        {
            return PdfDocument.Open(new System.IO.MemoryStream(pdf, writable: false), leaveOpen: true);
        }
    }

    private static byte[] ManyPages()
    {
        var builder = new PdfBuilder();
        var kids = new StringBuilder();
        for (int i = 0; i < PageTotal; i++)
        {
            kids.Append(i + 3).Append(" 0 R ");
            builder.Indirect(i + 3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        }

        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R >>");
        builder.Indirect(2, "<< /Type /Pages /Count " + PageTotal + " /Kids [" + kids + "] >>");
        return builder.Finish();
    }
}
