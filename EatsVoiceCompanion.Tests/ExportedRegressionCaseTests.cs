using System.Text.Json;
using EatsVoiceCompanion.App.Services;
using EatsVoiceCompanion.Core.Commands;
using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Tests;

public sealed class ExportedRegressionCaseTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IEnumerable<object[]> Cases()
    {
        string directory = Path.Combine(
            AppContext.BaseDirectory,
            "RegressionCases");

        return Directory.EnumerateFiles(directory, "*.json")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new object[]
            {
                Path.GetFileName(path),
                File.ReadAllText(path)
            })
            .ToArray();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ExportedCase_ProducesExpectedCommand(
        string fileName,
        string json)
    {
        CorrectionRegressionCase regressionCase =
            JsonSerializer.Deserialize<CorrectionRegressionCase>(
                json,
                SerializerOptions) ??
            throw new InvalidDataException(
                $"{fileName} did not contain a regression case.");
        CorrectionRegressionCaseValidator.Validate(regressionCase);
        IReadOnlySet<string> activeCallsigns = new HashSet<string>(
            [regressionCase.Callsign],
            StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, string> activeStars =
            string.IsNullOrWhiteSpace(regressionCase.ActiveStar)
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    [regressionCase.Callsign] = regressionCase.ActiveStar
                };
        IReadOnlyDictionary<string, IReadOnlySet<string>> routeFixes =
            regressionCase.RouteFixes.Length == 0
                ? new Dictionary<string, IReadOnlySet<string>>()
                : new Dictionary<string, IReadOnlySet<string>>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    [regressionCase.Callsign] = new HashSet<string>(
                        regressionCase.RouteFixes,
                        StringComparer.OrdinalIgnoreCase)
                };

        VoiceCommandInterpretation interpretation =
            new VoiceCommandInterpreter(regressionCase.AirlineAliases)
                .Interpret(
                    regressionCase.OriginalTranscript,
                    regressionCase.ControllerPosition,
                    activeCallsigns,
                    activeStars,
                    routeFixes);
        string actual = EatsTransmissionValidator.Validate(
            interpretation.Command.ToEatsCommand());

        Assert.True(
            string.Equals(
                regressionCase.ExpectedCommand,
                actual,
                StringComparison.Ordinal),
            $"{fileName} produced '{actual}' instead of " +
            $"'{regressionCase.ExpectedCommand}'.");
    }
}
