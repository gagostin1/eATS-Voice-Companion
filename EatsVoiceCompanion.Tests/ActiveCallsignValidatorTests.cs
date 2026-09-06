using EatsVoiceCompanion.Core.Safety;

namespace EatsVoiceCompanion.Tests;

public sealed class ActiveCallsignValidatorTests
{
    private static readonly string[] ActiveCallsigns =
    {
        "AAL1307",
        "DAL1380",
        "UPS1076"
    };

    [Fact]
    public void IsActive_ReturnsTrueForExactCallsign()
    {
        bool result =
            ActiveCallsignValidator.IsActive(
                "AAL1307",
                ActiveCallsigns);

        Assert.True(result);
    }

    [Fact]
    public void IsActive_IsCaseInsensitive()
    {
        bool result =
            ActiveCallsignValidator.IsActive(
                "dal1380",
                ActiveCallsigns);

        Assert.True(result);
    }

    [Fact]
    public void IsActive_ReturnsFalseForSimilarCallsign()
    {
        bool result =
            ActiveCallsignValidator.IsActive(
                "AAL1308",
                ActiveCallsigns);

        Assert.False(result);
    }

    [Fact]
    public void IsActive_ReturnsFalseForUnknownCallsign()
    {
        bool result =
            ActiveCallsignValidator.IsActive(
                "UAL714",
                ActiveCallsigns);

        Assert.False(result);
    }

    [Fact]
    public void IsActive_RejectsBlankCallsign()
    {
        Assert.Throws<ArgumentException>(
            () => ActiveCallsignValidator.IsActive(
                "",
                ActiveCallsigns));
    }
}