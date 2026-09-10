using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class ApproachIdNormalizerTests
{
    [Theory]
    [InlineData("ILS runway two five left approach", "ILS25L")]
    [InlineData("RNAV Yankee runway three one", "RNAVY31")]
    [InlineData("ILS Zulu runway one six left", "ILSZ16L")]
    [InlineData("localizer back course runway one three", "LOCBC13")]
    [InlineData("VOR Alpha", "VORA")]
    [InlineData("visual approach runway five", "VA5")]
    [InlineData("RNAVY31", "RNAVY31")]
    public void Normalize_ReturnsDocumentedDatabaseIdentifier(
        string spoken,
        string expected)
    {
        Assert.Equal(expected, ApproachIdNormalizer.Normalize(spoken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("runway two five left")]
    [InlineData("PAR runway two five")]
    [InlineData("ILS")]
    public void Normalize_RejectsMissingOrUnsupportedApproach(string value)
    {
        Assert.Throws<ArgumentException>(
            () => ApproachIdNormalizer.Normalize(value));
    }
}
