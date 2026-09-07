using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Commands;

public static partial class EatsTransmissionValidator
{
    public static string Validate(string transmission)
    {
        if (string.IsNullOrWhiteSpace(transmission))
        {
            throw new ArgumentException(
                "A transmission is required.",
                nameof(transmission));
        }

        string upper = transmission.ToUpperInvariant();

        if (upper.Length > 120 ||
            !AllowedCharactersPattern().IsMatch(upper))
        {
            throw new ArgumentException(
                "The transmission contains unsupported characters " +
                "or is too long.",
                nameof(transmission));
        }

        string normalized = Regex.Replace(
            upper.Trim(),
            " +",
            " ");

        string[] tokens = normalized.Split(' ');

        if (tokens.Length < 2 ||
            !CallsignPattern().IsMatch(tokens[0]))
        {
            throw new ArgumentException(
                "The transmission must begin with a valid callsign " +
                "and contain at least one instruction.",
                nameof(transmission));
        }

        string[] instructions = tokens.Skip(1).ToArray();

        foreach (string instruction in instructions)
        {
            if (!IsAllowedInstruction(instruction))
            {
                throw new ArgumentException(
                    $"'{instruction}' is not an allowed eATS command.",
                    nameof(transmission));
            }
        }

        ValidateInstructionOrder(instructions);

        return normalized;
    }

    private static bool IsAllowedInstruction(string instruction)
    {
        if (instruction is "R" or "DV" ||
            DirectPattern().IsMatch(instruction) ||
            AltimeterPattern().IsMatch(instruction) ||
            CrossAtPattern().IsMatch(instruction) &&
                IsValidCrossAt(instruction))
        {
            return true;
        }

        return IsFormattedNumber(
                   instruction,
                   "FH",
                   EatsCommandFormatter.FlyHeading) ||
               IsFormattedNumber(
                   instruction,
                   "TLH",
                   EatsCommandFormatter.TurnLeftHeading) ||
               IsFormattedNumber(
                   instruction,
                   "TRH",
                   EatsCommandFormatter.TurnRightHeading) ||
               IsFormattedNumber(
                   instruction,
                   "CM",
                   value => EatsCommandFormatter.ClimbAndMaintain(
                       checked(value * 100))) ||
               IsFormattedNumber(
                   instruction,
                   "DM",
                   value => EatsCommandFormatter.DescendAndMaintain(
                       checked(value * 100))) ||
               IsFormattedNumber(
                   instruction,
                   "DVXM",
                   value => EatsCommandFormatter.DescendViaExceptMaintain(
                       checked(value * 100))) ||
               IsFormattedNumber(
                   instruction,
                   "S",
                   EatsCommandFormatter.MaintainSpeed);
    }

    private static bool IsValidCrossAt(string instruction)
    {
        Match match = CrossAtPattern().Match(instruction);

        if (!match.Success ||
            !int.TryParse(match.Groups["altitude"].Value, out int altitude))
        {
            return false;
        }

        string fix = match.Groups["fix"].Value;
        string speedText = match.Groups["speed"].Value;

        try
        {
            if (speedText.Length == 0)
            {
                return string.Equals(
                    instruction,
                    EatsCommandFormatter.CrossAtAltitude(
                        fix,
                        checked(altitude * 100)),
                    StringComparison.Ordinal);
            }

            return int.TryParse(speedText, out int speed) &&
                   string.Equals(
                       instruction,
                       EatsCommandFormatter.CrossAtAltitudeAndSpeed(
                           fix,
                           checked(altitude * 100),
                           speed),
                       StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static void ValidateInstructionOrder(string[] instructions)
    {
        int descendViaIndex = Array.FindIndex(
            instructions,
            instruction => instruction == "DV" ||
                           instruction.StartsWith(
                               "DVXM",
                               StringComparison.Ordinal));

        if (descendViaIndex <= 0)
        {
            return;
        }

        bool speedBeforeDescendVia = instructions
            .Take(descendViaIndex)
            .Any(instruction => SpeedPattern().IsMatch(instruction));

        if (speedBeforeDescendVia)
        {
            throw new ArgumentException(
                "A descend-via command must precede an assigned speed " +
                "because eATS cancels earlier speed assignments.",
                nameof(instructions));
        }
    }

    private static bool IsFormattedNumber(
        string instruction,
        string prefix,
        Func<int, string> formatter)
    {
        if (!instruction.StartsWith(
                prefix,
                StringComparison.Ordinal) ||
            !int.TryParse(instruction[prefix.Length..], out int value))
        {
            return false;
        }

        try
        {
            return string.Equals(
                instruction,
                formatter(value),
                StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    [GeneratedRegex("\\A[A-Z0-9.@ ]+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedCharactersPattern();

    [GeneratedRegex("^[A-Z0-9]{2,7}$", RegexOptions.CultureInvariant)]
    private static partial Regex CallsignPattern();

    [GeneratedRegex(
        "^\\.\\.[A-Z0-9]{2,8}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex DirectPattern();

    [GeneratedRegex("^A[0-9]{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex AltimeterPattern();

    [GeneratedRegex(
        "^X(?<fix>[A-Z0-9]{2,8})@(?<altitude>[0-9]{2,3})(?:@(?<speed>[0-9]{3})K)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CrossAtPattern();

    [GeneratedRegex("^(?:M?S)[0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex SpeedPattern();
}
