using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Commands;

public static class EatsCommandFormatter
{
    private static readonly Regex CallsignPattern =
        new("^[A-Z0-9]{2,7}$", RegexOptions.Compiled);

    private static readonly Regex FixPattern =
        new("^[A-Z0-9]{2,8}$", RegexOptions.Compiled);

    public static string BuildTransmission(
        string callsign,
        params string[] instructions)
    {
        string normalizedCallsign = NormalizeCallsign(callsign);

        if (instructions is null || instructions.Length == 0)
        {
            throw new ArgumentException(
                "At least one instruction is required.",
                nameof(instructions));
        }

        string[] normalizedInstructions = instructions
            .Select(NormalizeInstruction)
            .ToArray();

        return string.Join(
            ' ',
            new[] { normalizedCallsign }.Concat(normalizedInstructions));
    }

    public static string Roger()
    {
        return "R";
    }
    public static string FlyHeading(int heading)
    {
        return $"FH{FormatHeading(heading)}";
    }

    public static string TurnLeftHeading(int heading)
    {
        return $"TLH{FormatHeading(heading)}";
    }

    public static string TurnRightHeading(int heading)
    {
        return $"TRH{FormatHeading(heading)}";
    }

    public static string ClimbAndMaintain(int altitudeFeet)
    {
        return $"CM{FormatAltitude(altitudeFeet)}";
    }

    public static string DescendAndMaintain(int altitudeFeet)
    {
        return $"DM{FormatAltitude(altitudeFeet)}";
    }

    public static string MaintainSpeed(int speedKnots)
    {
        if (speedKnots is < 100 or > 350)
        {
            throw new ArgumentOutOfRangeException(
                nameof(speedKnots),
                "Speed must be between 100 and 350 knots.");
        }

        return $"S{speedKnots}";
    }

    public static string ProceedDirect(string fix)
    {
        string normalizedFix = fix.Trim().ToUpperInvariant();

        if (!FixPattern.IsMatch(normalizedFix))
        {
            throw new ArgumentException(
                "A fix must contain 2-8 letters or digits.",
                nameof(fix));
        }

        return $"..{normalizedFix}";
    }

    private static string NormalizeCallsign(string callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign))
        {
            throw new ArgumentException(
                "A callsign is required.",
                nameof(callsign));
        }

        string normalized = callsign.Trim().ToUpperInvariant();

        if (!CallsignPattern.IsMatch(normalized))
        {
            throw new ArgumentException(
                "A callsign must contain 2-7 letters or digits.",
                nameof(callsign));
        }

        return normalized;
    }

    private static string NormalizeInstruction(string instruction)
    {
        if (string.IsNullOrWhiteSpace(instruction))
        {
            throw new ArgumentException(
                "Instructions cannot be empty.",
                nameof(instruction));
        }

        string normalized = instruction.Trim().ToUpperInvariant();

        if (normalized.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "A single instruction cannot contain spaces.",
                nameof(instruction));
        }

        return normalized;
    }

    private static string FormatHeading(int heading)
    {
        if (heading is < 1 or > 360)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heading),
                "Heading must be between 1 and 360 degrees.");
        }

        return heading.ToString("D3");
    }

    private static string FormatAltitude(int altitudeFeet)
    {
        if (altitudeFeet is < 1000 or > 60000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(altitudeFeet),
                "Altitude must be between 1,000 and 60,000 feet.");
        }

        if (altitudeFeet % 100 != 0)
        {
            throw new ArgumentException(
                "Altitude must be specified in whole hundreds of feet.",
                nameof(altitudeFeet));
        }

        return (altitudeFeet / 100).ToString("D2");
    }
}