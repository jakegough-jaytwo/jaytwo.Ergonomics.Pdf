using System.Linq;
using Xunit;

namespace jaytwo.Pdf.Tests;

// W-133 (reset-plan R1 step 5): the engine's public types are semantic models; the raw object model stays in jaytwo.Pdf.Internal.
public class PublicApiTests
{
    [Fact]
    public void public_types_live_only_in_the_semantic_namespaces()
    {
        string[] namespaces = { "jaytwo.Pdf", "jaytwo.Pdf.AcroForm", "jaytwo.Pdf.Attachments", "jaytwo.Pdf.Metadata", "jaytwo.Pdf.Signatures" };

        string[] stray = typeof(PdfDocument).Assembly.GetExportedTypes()
            .Where(type => !namespaces.Contains(type.Namespace))
            .Select(type => type.FullName!)
            .ToArray();

        Assert.Empty(stray);
    }
}
