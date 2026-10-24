using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using jaytwo.Pdf.Internal;
using Xunit;

namespace jaytwo.Pdf.Tests;

// W-167: the internal work counters count what they name. Four fields, each in its own object stream behind two filters.
public class WorkCounterTests
{
    [Fact]
    public void field_walks_count_stages_parses_evictions_loads_and_visits()
    {
        var builder = new PdfBuilder();
        builder.Indirect(1, "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [10 0 R 12 0 R 14 0 R 16 0 R] >> >>");
        builder.Indirect(2, "<< /Type /Pages /Count 1 /Kids [3 0 R] >>");
        builder.Indirect(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [10 0 R 12 0 R 14 0 R 16 0 R] >>");
        long stageBytes = 0;
        for (int i = 0; i < 4; i++)
        {
            int fieldId = 10 + (i * 2);
            int streamId = 20 + i;
            byte[] data = ObjectStream(
                out int first,
                (fieldId, "<< /Subtype /Widget /FT /Tx /T (F" + i.ToString(CultureInfo.InvariantCulture) + ") /Rect [0 0 72 16] /P 3 0 R >>"),
                (fieldId + 1, "<< /Pad true >>"));
            byte[] flate = TestCompat.Zlib(data);
            byte[] hex = Encoding.ASCII.GetBytes(TestCompat.ToHexString(flate) + ">");
            stageBytes += flate.Length + data.Length;
            builder.StreamObject(streamId, "/Type /ObjStm /N 2 /First " + first.ToString(CultureInfo.InvariantCulture) + " /Filter [/ASCIIHexDecode /FlateDecode]", hex);
            builder.Compressed(fieldId, streamId, 0);
            builder.Compressed(fieldId + 1, streamId, 1);
        }

        builder.AppendXrefStream(30, string.Empty);
        using var document = PdfDocument.Open(new MemoryStream(builder.Snapshot(), writable: false), leaveOpen: true);
        PdfWorkCounters work = document.WorkCounters;
        long openStages = work.FilterStageDecodes;
        long openStageBytes = work.FilterStageBytes;
        long openLoads = work.ObjectLoads;

        Assert.Equal(4, document.AcroForm.Fields.Count);

        Assert.Equal(4, work.FieldNodeVisits);
        Assert.Equal(2, work.PageNodeVisits);
        Assert.Equal(4, work.ObjectStreamParses);
        Assert.Equal(8, work.FilterStageDecodes - openStages);
        Assert.Equal(stageBytes, work.FilterStageBytes - openStageBytes);

        // D-49: each new object stream releases the one before it, and the operation's end releases the last.
        Assert.Equal(4, work.ObjectStreamEvictions);
        Assert.Equal(4, work.CachedObjectEvictions);
        long firstWalkLoads = work.ObjectLoads - openLoads;
        Assert.True(firstWalkLoads >= 8, "the first walk loaded " + firstWalkLoads.ToString(CultureInfo.InvariantCulture) + " objects");

        // A second walk decodes every object stream again and reloads the four fields, but not the cached stream dictionaries.
        long resolutions = work.ReferenceResolutions;
        Assert.Equal(4, document.AcroForm.Fields.Count);
        Assert.Equal(8, work.ObjectStreamParses);
        Assert.Equal(16, work.FilterStageDecodes - openStages);
        Assert.Equal(8, work.FieldNodeVisits);
        Assert.Equal(4, work.ObjectLoads - openLoads - firstWalkLoads);
        Assert.True(work.ReferenceResolutions > resolutions);
    }

    private static byte[] ObjectStream(out int first, params (int Id, string Body)[] objects)
    {
        var header = new StringBuilder();
        var body = new StringBuilder();
        foreach ((int id, string item) in objects)
        {
            header.Append(id.ToString(CultureInfo.InvariantCulture)).Append(' ');
            header.Append(body.Length.ToString(CultureInfo.InvariantCulture)).Append(' ');
            body.Append(item);
        }

        first = header.Length;
        return Encoding.ASCII.GetBytes(header.ToString() + body.ToString());
    }
}
