using System.IO;
using EatsVoiceCompanion.Core.Commands;

namespace EatsVoiceCompanion.App.Services;

public static class CorrectionRegressionCaseValidator
{
    public const int CurrentSchemaVersion = 2;

    public static void Validate(CorrectionRegressionCase regressionCase)
    {
        ArgumentNullException.ThrowIfNull(regressionCase);

        if (regressionCase.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Regression schema {regressionCase.SchemaVersion} is not " +
                $"supported; expected {CurrentSchemaVersion}.");
        }

        Require(regressionCase.AppVersion, "AppVersion");
        Require(regressionCase.Callsign, "Callsign");
        Require(regressionCase.OriginalTranscript, "OriginalTranscript");
        Require(regressionCase.CorrectedTranscript, "CorrectedTranscript");
        Require(regressionCase.ExpectedCommand, "ExpectedCommand");
        ArgumentNullException.ThrowIfNull(regressionCase.ControllerPosition);

        if (!IsCanonicalToken(regressionCase.Callsign))
        {
            throw new InvalidDataException(
                "Callsign must contain uppercase letters and digits only.");
        }

        string expected = EatsTransmissionValidator.Validate(
            regressionCase.ExpectedCommand);
        string expectedCallsign = expected.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries)[0];

        if (!string.Equals(
                regressionCase.Callsign,
                expectedCallsign,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Callsign must match the first token of ExpectedCommand.");
        }

        if (regressionCase.AirlineAliases is null)
        {
            throw new InvalidDataException("AirlineAliases is required.");
        }

        if (regressionCase.RouteFixes is null)
        {
            throw new InvalidDataException("RouteFixes is required.");
        }

        if (!regressionCase.Callsign.StartsWith('N') &&
            regressionCase.AirlineAliases.Count == 0)
        {
            throw new InvalidDataException(
                "An airline callsign requires its relevant spoken alias.");
        }

        foreach ((string spoken, string code) in
                 regressionCase.AirlineAliases)
        {
            Require(spoken, "AirlineAliases key");
            Require(code, "AirlineAliases value");

            if (!IsCanonicalToken(code))
            {
                throw new InvalidDataException(
                    "Airline alias values must contain uppercase letters " +
                    "and digits only.");
            }

            if (!regressionCase.Callsign.StartsWith(
                    code,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Airline alias '{spoken}' does not match callsign " +
                    $"{regressionCase.Callsign}.");
            }
        }

        if (regressionCase.RouteFixes.Any(fix =>
                !IsCanonicalToken(fix)))
        {
            throw new InvalidDataException(
                "RouteFixes must contain uppercase letters and digits only.");
        }

        if (regressionCase.RouteFixes.Distinct(
                StringComparer.OrdinalIgnoreCase).Count() !=
            regressionCase.RouteFixes.Length)
        {
            throw new InvalidDataException("RouteFixes must be unique.");
        }
    }

    private static void Require(string? value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"{propertyName} is required.");
        }
    }

    private static bool IsCanonicalToken(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(character =>
            char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character));
}
