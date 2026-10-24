using System;
using jaytwo.Pdf;
using Xunit;

namespace jaytwo.Ergonomics.Pdf.Tests;

// D-54: the dependency runs one way, jaytwo.Ergonomics.Pdf -> jaytwo.Pdf.
public class EngineBoundaryTests
{
    [Fact]
    public void engine_assembly_does_not_reference_ergonomics()
    {
        var engine = typeof(PdfDocument).Assembly;

        Assert.Equal("jaytwo.Pdf", engine.GetName().Name);
        Assert.DoesNotContain(engine.GetReferencedAssemblies(), reference => reference.Name!.StartsWith("jaytwo.Ergonomics", StringComparison.Ordinal));
    }
}
