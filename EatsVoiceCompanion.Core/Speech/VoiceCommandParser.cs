using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Speech;

public sealed class VoiceCommandParser
{
    private static readonly CommandPattern[] Patterns =
    {
        new(
            "climb and maintain flight level",
            VoiceInstructionType.ClimbAndMaintain,
            IsFlightLevel: true),

        new(
            "descend and maintain flight level",
            VoiceInstructionType.DescendAndMaintain,
            IsFlightLevel: true),

        new(
            "turn left heading",
            VoiceInstructionType.TurnLeftHeading),

        new(
            "turn right heading",
            VoiceInstructionType.TurnRightHeading),

        new(
            "fly heading",
            VoiceInstructionType.FlyHeading),

        new(
            "climb and maintain",
            VoiceInstructionType.ClimbAndMaintain),

        new(
            "descend and maintain",
            VoiceInstructionType.DescendAndMaintain),

        new(
            "maintain speed",
            VoiceInstructionType.MaintainSpeed),

        new(
            "proceed direct to",
            VoiceInstructionType.ProceedDirect),

        new(
            "proceed direct",
            VoiceInstructionType.ProceedDirect)
    };

    private readonly AirlineCallsignParser _callsignParser;

    public VoiceCommandParser(
        IReadOnlyDictionary<string, string> airlineAliases)
    {
        _callsignParser =
            new AirlineCallsignParser(airlineAliases);
    }

    public ParsedVoiceCommand Parse(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            throw new ArgumentException(
                "A transcript is required.",
                nameof(transcript));
        }

        string normalized = NormalizeWords(transcript);

        foreach (CommandPattern pattern in Patterns)
        {
            string separator = $" {pattern.Phrase} ";

            int separatorIndex = normalized.IndexOf(
                separator,
                StringComparison.OrdinalIgnoreCase);

            if (separatorIndex <= 0)
            {
                continue;
            }

            string callsignText =
                normalized[..separatorIndex];

            string valueText =
                normalized[
                    (separatorIndex + separator.Length)..];

            if (string.IsNullOrWhiteSpace(valueText))
            {
                throw new ArgumentException(
                    "The instruction value is missing.",
                    nameof(transcript));
            }

            string callsign =
                _callsignParser.Parse(callsignText);

            if (pattern.InstructionType ==
                VoiceInstructionType.ProceedDirect)
            {
                return new ParsedVoiceCommand(
                    callsign,
                    pattern.InstructionType,
                    TextValue: ParseFix(valueText));
            }

            int numericValue;

            if (pattern.IsFlightLevel)
            {
                int flightLevel =
                    AviationNumberParser.Parse(valueText);

                numericValue = checked(flightLevel * 100);
            }
            else if (pattern.InstructionType is
                    VoiceInstructionType.ClimbAndMaintain or
                    VoiceInstructionType.DescendAndMaintain)
            {
                numericValue =
                    AviationAltitudeParser.Parse(valueText);
            }
            else
            {
                numericValue =
                    AviationNumberParser.Parse(valueText);
            }

            return new ParsedVoiceCommand(
                callsign,
                pattern.InstructionType,
                NumericValue: numericValue);
        }

        throw new InvalidOperationException(
            "No supported instruction was recognized.");
    }

    private static string ParseFix(string value)
    {
        if (value.Contains(' '))
        {
            throw new ArgumentException(
                "A direct-to fix must be recognized " +
                "as one continuous word.",
                nameof(value));
        }

        return value.ToUpperInvariant();
    }

    private static string NormalizeWords(string value)
    {
        string normalized = Regex.Replace(
            value.Trim().ToLowerInvariant(),
            @"[^\p{L}\p{N}]+",
            " ");

        return Regex.Replace(
            normalized,
            @"\s+",
            " ").Trim();
    }

    private sealed record CommandPattern(
        string Phrase,
        VoiceInstructionType InstructionType,
        bool IsFlightLevel = false);
}