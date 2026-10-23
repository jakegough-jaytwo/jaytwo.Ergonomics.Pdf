using System;

namespace jaytwo.Ergonomics.Pdf.Tests;

// D-52: tests are classified by what they touch, not by how long they take. The fast lane (make quiet-test LANE=fast)
// leaves out every category below and sets JAYTWO_LANE=fast, and the helpers that start tools, load iText, or open
// corpus files refuse there, so an unclassified test fails the fast lane instead of slowing it.
internal static class Lane
{
    public const string Trait = "Category";

    // An implementation written elsewhere judges what the library wrote or read. Runs on net10.0 only (W-123).
    public const string Interop = "Interop";

    // Starts an external tool or loads a native tool library only to make input or set up.
    public const string External = "External";

    // Opens G2 corpus files.
    public const string Corpus = "Corpus";

    // Builds or reads data sized to reach a cap, a budget, or an allocation bound.
    public const string LargeData = "LargeData";

    // Repeats an operation across every offset, failure point, or fixture.
    public const string Sweep = "Sweep";

    public static bool IsFast => Environment.GetEnvironmentVariable("JAYTWO_LANE") == "fast";

    public static void Refuse(string resource, string traits)
    {
        if (IsFast)
        {
            throw new InvalidOperationException(resource + " is outside the fast lane. Mark the test " + traits + " (D-52).");
        }
    }
}
