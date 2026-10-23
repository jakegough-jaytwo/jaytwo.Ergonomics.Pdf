using Xunit;

namespace jaytwo.Ergonomics.Pdf.Comparison;

[Collection("comparison")]
public class FixtureSetup
{
    // `make compare-itext` runs this first, in its own process, so no measured process writes a fixture.
    [Fact]
    public void prepare()
    {
        ComparisonFixtures.Prepare();
    }
}
