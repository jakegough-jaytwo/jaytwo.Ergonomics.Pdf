using System;
using System.Linq;
using System.Runtime.CompilerServices;
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

    [Fact]
    public void ergonomics_assembly_references_only_the_engine_and_the_framework()
    {
        var ergonomics = typeof(PdfWorkflow).Assembly;

        Assert.Equal("jaytwo.Ergonomics.Pdf", ergonomics.GetName().Name);
        Assert.Equal(
            new[] { "jaytwo.Pdf" },
            ergonomics.GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(name => name.StartsWith("jaytwo.", StringComparison.Ordinal))
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal));
        Assert.DoesNotContain(
            ergonomics.GetReferencedAssemblies(),
            reference => !IsFramework(reference.Name!) && reference.Name != "jaytwo.Pdf");
    }

    [Fact]
    public void engine_grants_the_ergonomics_assembly_no_internals()
    {
        var granted = typeof(PdfDocument).Assembly
            .GetCustomAttributes(typeof(InternalsVisibleToAttribute), inherit: false)
            .Cast<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .ToArray();

        Assert.DoesNotContain(granted, name => name.StartsWith("jaytwo.Ergonomics.Pdf,", StringComparison.Ordinal) || name == "jaytwo.Ergonomics.Pdf");
        Assert.DoesNotContain(granted, name => name.StartsWith("jaytwo.Ergonomics.Pdf.Tests", StringComparison.Ordinal));
    }

    // The runtime dependency set is closed (AGENTS.md): the framework plus the netstandard2.0 compatibility packages.
    private static bool IsFramework(string name)
    {
        return name == "mscorlib"
            || name == "netstandard"
            || name.StartsWith("System", StringComparison.Ordinal)
            || name == "Microsoft.Bcl.AsyncInterfaces";
    }
}
