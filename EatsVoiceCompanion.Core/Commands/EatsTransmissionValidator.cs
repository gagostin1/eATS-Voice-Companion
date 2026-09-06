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

        foreach (string instruction in tokens.Skip(1))
        {
            if (!IsAllowedInstruction(instruction))
            {
                throw new ArgumentException(
                    $"'{instruction}' is not an allowed eATS command.",
                    nameof(transmission));
            }
        }

        return normalized;
    }

    private static bool IsAllowedInstruction(string instruction)
    {
        if (instruction == "R" || DirectPattern().IsMatch(instruction))
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
                   "S",
                   EatsCommandFormatter.MaintainSpeed);
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

    [GeneratedRegex("\\A[A-Z0-9. ]+\\z", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedCharactersPattern();

    [GeneratedRegex("^[A-Z0-9]{2,7}$", RegexOptions.CultureInvariant)]
    private static partial Regex CallsignPattern();

    [GeneratedRegex(
        "^\\.\\.[A-Z0-9]{2,8}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex DirectPattern();
}
