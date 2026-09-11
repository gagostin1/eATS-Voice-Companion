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

        ValidateExpediteOrder(instructions);
        ValidateInstructionOrder(instructions);
        ValidateApproachOrder(instructions);
        ValidateCommunicationOrder(instructions);
        ValidateTransponderInstructions(instructions);
        ValidateCrossDistanceInstructions(instructions);

        return normalized;
    }

    private static bool IsAllowedInstruction(string instruction)
    {
        if (instruction is "R" or "DV" or "EXP" or "SA" or
                "RNS" or "SI" or "SM" or "SNS" or
                "PH" or "INTC" or "CA" or "SAR" or "S-" or
                "*0" or "?" or "SBY" or
                "ID" or "SQALT" or "SQNORM" or "SQSBY" or
                "SQVFR" or "STOPALTSQ" ||
            TransponderCodePattern().IsMatch(instruction) ||
            DirectPattern().IsMatch(instruction) ||
            ContactFrequencyPattern().IsMatch(instruction) &&
                IsValidContactFrequency(instruction) ||
            ExpectedApproachPattern().IsMatch(instruction) &&
                IsValidExpectedApproach(instruction) ||
            AltimeterPattern().IsMatch(instruction) ||
            PublishedSpeedPattern().IsMatch(instruction) ||
            CrossAtPattern().IsMatch(instruction) &&
                IsValidCrossAt(instruction) ||
            CrossDistancePattern().IsMatch(instruction) &&
                IsValidCrossDistance(instruction))
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
                   "PD",
                   value => EatsCommandFormatter.DescendAtPilotsDiscretion(
                       checked(value * 100))) ||
               IsFormattedNumber(
                   instruction,
                   "EXP",
                   value => EatsCommandFormatter.ExpediteThroughAltitude(
                       checked(value * 100))) ||
               IsFormattedNumber(
                   instruction,
                   "RL",
                   value => EatsCommandFormatter.ReportLeavingAltitude(
                       checked(value * 100))) ||
               IsFormattedNumber(
                   instruction,
                   "RR",
                   value => EatsCommandFormatter.ReportReachingAltitude(
                       checked(value * 100))) ||
               IsValidSpeed(instruction) ||
               IsValidMach(instruction);
    }

    private static void ValidateExpediteOrder(string[] instructions)
    {
        int[] expediteIndexes = instructions
            .Select((instruction, index) => (instruction, index))
            .Where(item => ExpeditePattern().IsMatch(item.instruction))
            .Select(item => item.index)
            .ToArray();

        if (expediteIndexes.Length == 0)
        {
            return;
        }

        if (expediteIndexes.Length > 1)
        {
            throw new ArgumentException(
                "Only one expedite instruction can be staged at a time.",
                nameof(instructions));
        }

        if (instructions.Any(instruction =>
                instruction == "DV" ||
                instruction.StartsWith("DVXM", StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "eATS does not accept expedite while descending via.",
                nameof(instructions));
        }

        if (instructions.Contains("CA", StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "eATS does not accept expedite with an approach clearance.",
                nameof(instructions));
        }

        if (instructions.Any(instruction =>
                instruction.StartsWith("PD", StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Pilot's-discretion descent and expedite cannot be " +
                "combined because expedite converts the descent to " +
                "descend-and-maintain behavior in eATS.",
                nameof(instructions));
        }

        int expediteIndex = expediteIndexes[0];

        if (instructions
            .Skip(expediteIndex + 1)
            .Any(IsAltitudeCommand))
        {
            throw new ArgumentException(
                "Expedite must follow the final altitude command because " +
                "a later altitude command cancels it in eATS.",
                nameof(instructions));
        }
    }

    private static bool IsAltitudeCommand(string instruction)
    {
        return instruction is "DV" or "CA" ||
               instruction.StartsWith("CM", StringComparison.Ordinal) ||
               instruction.StartsWith("DM", StringComparison.Ordinal) ||
               instruction.StartsWith("DVXM", StringComparison.Ordinal) ||
               instruction.StartsWith("PD", StringComparison.Ordinal) ||
               CrossAtPattern().IsMatch(instruction) ||
               CrossDistancePattern().IsMatch(instruction);
    }

    private static bool IsValidExpectedApproach(string instruction)
    {
        Match match = ExpectedApproachPattern().Match(instruction);

        if (!match.Success)
        {
            return false;
        }

        try
        {
            return instruction == EatsCommandFormatter.ExpectApproach(
                match.Groups["approach"].Value);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsValidContactFrequency(string instruction)
    {
        Match match = ContactFrequencyPattern().Match(instruction);

        if (!match.Success)
        {
            return false;
        }

        string abbreviated = match.Groups["frequency"].Value;
        string fullDigits = "1" + abbreviated;
        string frequency = fullDigits[..3] + "." + fullDigits[3..];

        try
        {
            return instruction ==
                   EatsCommandFormatter.ContactFrequency(frequency);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void ValidateCommunicationOrder(string[] instructions)
    {
        int[] frequencyIndexes = instructions
            .Select((instruction, index) => (instruction, index))
            .Where(item => item.instruction == "*0" ||
                ContactFrequencyPattern().IsMatch(item.instruction))
            .Select(item => item.index)
            .ToArray();

        if (frequencyIndexes.Length > 1)
        {
            throw new ArgumentException(
                "Only one frequency instruction can be staged at a time.",
                nameof(instructions));
        }

        if (frequencyIndexes.Length == 1 &&
            frequencyIndexes[0] != instructions.Length - 1)
        {
            throw new ArgumentException(
                "A frequency instruction must be the final token in the " +
                "preview.",
                nameof(instructions));
        }

        if (instructions.Length > 1 &&
            instructions.Any(instruction => instruction is "?" or "SBY"))
        {
            throw new ArgumentException(
                "Say-again and stand-by responses must be staged alone.",
                nameof(instructions));
        }
    }

    private static void ValidateTransponderInstructions(
        string[] instructions)
    {
        string[] transponderInstructions = instructions
            .Where(IsTransponderInstruction)
            .ToArray();

        if (transponderInstructions.Length == 0)
        {
            return;
        }

        int codeSelections = transponderInstructions.Count(
            instruction =>
                TransponderCodePattern().IsMatch(instruction) ||
                instruction == "SQVFR");
        int operatingModes = transponderInstructions.Count(
            instruction => instruction is "SQNORM" or "SQSBY");
        int altitudeModes = transponderInstructions.Count(
            instruction => instruction is "SQALT" or "STOPALTSQ");
        int identCommands = transponderInstructions.Count(
            instruction => instruction == "ID");

        if (codeSelections > 1 || operatingModes > 1 ||
            altitudeModes > 1 || identCommands > 1)
        {
            throw new ArgumentException(
                "Conflicting or duplicate transponder instructions " +
                "cannot be staged together.",
                nameof(instructions));
        }

        if (transponderInstructions.Contains("SQSBY") &&
            transponderInstructions.Length > 1)
        {
            throw new ArgumentException(
                "Squawk standby must be staged without another " +
                "transponder instruction.",
                nameof(instructions));
        }
    }

    private static bool IsTransponderInstruction(string instruction)
    {
        return instruction is
                   "ID" or "SQALT" or "SQNORM" or "SQSBY" or
                   "SQVFR" or "STOPALTSQ" ||
               TransponderCodePattern().IsMatch(instruction);
    }

    private static void ValidateApproachOrder(string[] instructions)
    {
        int clearanceCount = instructions.Count(
            instruction => instruction == "CA");
        int expectedApproachCount = instructions.Count(
            instruction => ExpectedApproachPattern().IsMatch(instruction));

        if (clearanceCount > 1 || expectedApproachCount > 1)
        {
            throw new ArgumentException(
                "Only one approach clearance and one expected approach " +
                "can be staged at a time.",
                nameof(instructions));
        }

        int[] approachSpeedIndexes = instructions
            .Select((instruction, index) => (instruction, index))
            .Where(item => item.instruction == "S-")
            .Select(item => item.index)
            .ToArray();

        if (approachSpeedIndexes.Length > 1 ||
            approachSpeedIndexes.Length == 1 &&
            !instructions
                .Take(approachSpeedIndexes[0])
                .Contains("CA", StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Reduce to final approach speed requires CA earlier in " +
                "the same preview.",
                nameof(instructions));
        }

        int[] interceptIndexes = instructions
            .Select((instruction, index) => (instruction, index))
            .Where(item => item.instruction == "INTC")
            .Select(item => item.index)
            .ToArray();

        if (interceptIndexes.Length > 1)
        {
            throw new ArgumentException(
                "Only one final-approach intercept can be staged at a time.",
                nameof(instructions));
        }

        if (interceptIndexes.Length == 1)
        {
            int interceptIndex = interceptIndexes[0];

            if (!instructions.Take(interceptIndex).Any(IsHeadingCommand))
            {
                throw new ArgumentException(
                    "Intercept final approach course requires an explicit " +
                    "heading earlier in the same preview.",
                    nameof(instructions));
            }

            int clearanceIndex = Array.IndexOf(instructions, "CA");

            if (clearanceIndex >= 0 && clearanceIndex < interceptIndex)
            {
                throw new ArgumentException(
                    "The approach clearance must follow the final-course " +
                    "intercept instruction in the same preview.",
                    nameof(instructions));
            }
        }

        int lastClearanceIndex = Array.LastIndexOf(instructions, "CA");

        if (lastClearanceIndex < 0)
        {
            return;
        }

        if (instructions.Take(lastClearanceIndex).Any(IsAssignedSpeedOrMach))
        {
            throw new ArgumentException(
                "An approach clearance must precede an assigned speed or " +
                "Mach because eATS cancels earlier assignments.",
                nameof(instructions));
        }

        if (instructions
            .Skip(lastClearanceIndex + 1)
            .Any(instruction => instruction == "DV" ||
                instruction.StartsWith("DVXM", StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "A descend-via command after CA cancels the approach " +
                "clearance in eATS.",
                nameof(instructions));
        }

        int expectedApproachIndex = Array.FindIndex(
            instructions,
            instruction => ExpectedApproachPattern().IsMatch(instruction));

        if (expectedApproachIndex > lastClearanceIndex)
        {
            throw new ArgumentException(
                "Expect approach must precede CA because it cancels an " +
                "existing approach clearance.",
                nameof(instructions));
        }
    }

    private static bool IsHeadingCommand(string instruction) =>
        instruction == "PH" ||
        instruction.StartsWith("FH", StringComparison.Ordinal) ||
        instruction.StartsWith("TLH", StringComparison.Ordinal) ||
        instruction.StartsWith("TRH", StringComparison.Ordinal);

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

    private static bool IsValidCrossDistance(string instruction)
    {
        Match match = CrossDistancePattern().Match(instruction);

        if (!match.Success ||
            !int.TryParse(match.Groups["distance"].Value, out int distance) ||
            !int.TryParse(match.Groups["altitude"].Value, out int altitude))
        {
            return false;
        }

        try
        {
            return string.Equals(
                instruction,
                EatsCommandFormatter.CrossDistanceAtAltitude(
                    distance,
                    match.Groups["direction"].Value,
                    match.Groups["fix"].Value,
                    checked(altitude * 100)),
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

    private static void ValidateCrossDistanceInstructions(
        string[] instructions)
    {
        int[] crossDistanceIndexes = instructions
            .Select((instruction, index) => (instruction, index))
            .Where(item => CrossDistancePattern().IsMatch(item.instruction))
            .Select(item => item.index)
            .ToArray();

        if (crossDistanceIndexes.Length == 0)
        {
            return;
        }

        if (crossDistanceIndexes.Length > 1)
        {
            throw new ArgumentException(
                "Only one cross-distance restriction can be staged at a " +
                "time because a later one clears the earlier altitude.",
                nameof(instructions));
        }

        if (instructions
            .Skip(crossDistanceIndexes[0] + 1)
            .Any(instruction => DirectPattern().IsMatch(instruction)))
        {
            throw new ArgumentException(
                "A direct command after a cross-distance restriction " +
                "removes the restriction in eATS.",
                nameof(instructions));
        }
    }

    private static void ValidateInstructionOrder(string[] instructions)
    {
        int firstDescendViaIndex = Array.FindIndex(
            instructions,
            instruction => instruction == "DV" ||
                           instruction.StartsWith(
                               "DVXM",
                               StringComparison.Ordinal));

        if (firstDescendViaIndex > 0)
        {
            bool speedBeforeDescendVia = instructions
                .Take(firstDescendViaIndex)
                .Any(IsAssignedSpeedOrMach);

            if (speedBeforeDescendVia)
            {
                throw new ArgumentException(
                    "A descend-via command must precede an assigned speed " +
                    "or Mach because eATS cancels earlier assignments.",
                    nameof(instructions));
            }
        }

        int[] publishedSpeedIndexes = instructions
            .Select((instruction, index) => (instruction, index))
            .Where(item => PublishedSpeedPattern().IsMatch(item.instruction))
            .Select(item => item.index)
            .ToArray();

        if (publishedSpeedIndexes.Length == 0)
        {
            return;
        }

        if (publishedSpeedIndexes.Length > 1)
        {
            throw new ArgumentException(
                "Only one published-speed fix can be staged at a time.",
                nameof(instructions));
        }

        int publishedSpeedIndex = publishedSpeedIndexes[0];
        int lastDescendViaIndex = Array.FindLastIndex(
            instructions,
            instruction => instruction == "DV" ||
                           instruction.StartsWith(
                               "DVXM",
                               StringComparison.Ordinal));
        int lastDirectIndex = Array.FindLastIndex(
            instructions,
            instruction => DirectPattern().IsMatch(instruction));

        if (lastDescendViaIndex < 0 ||
            publishedSpeedIndex < lastDescendViaIndex ||
            publishedSpeedIndex < lastDirectIndex)
        {
            throw new ArgumentException(
                "Published-speed compliance must follow the final " +
                "descend-via and direct command in the same preview.",
                nameof(instructions));
        }

        if (instructions
            .Skip(publishedSpeedIndex + 1)
            .Any(IsAssignedSpeedOrMach))
        {
            throw new ArgumentException(
                "An assigned speed or Mach must precede published-speed " +
                "compliance in the same preview.",
                nameof(instructions));
        }
    }

    private static bool IsAssignedSpeedOrMach(string instruction) =>
        SpeedPattern().IsMatch(instruction) ||
        MachPattern().IsMatch(instruction);

    private static bool IsValidSpeed(string instruction) =>
        IsValidLimitedInstruction(
            instruction,
            SpeedPattern(),
            "value",
            EatsCommandFormatter.MaintainSpeed,
            EatsCommandFormatter.MaintainSpeedOrGreater,
            EatsCommandFormatter.MaintainSpeedOrLess);

    private static bool IsValidMach(string instruction) =>
        IsValidLimitedInstruction(
            instruction,
            MachPattern(),
            "value",
            EatsCommandFormatter.MaintainMach,
            EatsCommandFormatter.MaintainMachOrGreater,
            EatsCommandFormatter.MaintainMachOrLess);

    private static bool IsValidLimitedInstruction(
        string instruction,
        Regex pattern,
        string valueGroup,
        Func<int, string> exactFormatter,
        Func<int, string> greaterFormatter,
        Func<int, string> lessFormatter)
    {
        Match match = pattern.Match(instruction);

        if (!match.Success ||
            !int.TryParse(match.Groups[valueGroup].Value, out int value))
        {
            return false;
        }

        Func<int, string> formatter = match.Groups["limit"].Value switch
        {
            "+" => greaterFormatter,
            "-" => lessFormatter,
            _ => exactFormatter
        };

        try
        {
            return instruction == formatter(value);
        }
        catch (ArgumentException)
        {
            return false;
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

    [GeneratedRegex("\\A[A-Z0-9.@+*?\\- ]+\\z", RegexOptions.CultureInvariant)]
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
        "^CWS@[A-Z0-9]{2,8}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex PublishedSpeedPattern();

    [GeneratedRegex(
        "^E(?<approach>(?:ILS|RNAV|NDB|VOR|GPS|LDA|LOC|VA)[A-Z0-9]+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ExpectedApproachPattern();

    [GeneratedRegex(
        "^\\*(?<frequency>[0-9]{3,4})$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ContactFrequencyPattern();

    [GeneratedRegex(
        "^X(?<fix>[A-Z0-9]{2,8})@(?<altitude>[0-9]{2,3})(?:@(?<speed>[0-9]{3})K)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CrossAtPattern();

    [GeneratedRegex(
        "^X(?<distance>[0-9]{1,3})(?<direction>NE|SE|SW|NW|N|E|S|W)\\.(?<fix>[A-Z0-9]{2,8})@(?<altitude>[0-9]{2,3})$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CrossDistancePattern();

    [GeneratedRegex("^S(?<value>[0-9]{3})(?<limit>[+-]?)$", RegexOptions.CultureInvariant)]
    private static partial Regex SpeedPattern();

    [GeneratedRegex("^MM(?<value>[0-9]{2})(?<limit>[+-]?)$", RegexOptions.CultureInvariant)]
    private static partial Regex MachPattern();

    [GeneratedRegex("^EXP(?:[0-9]{2,3})?$", RegexOptions.CultureInvariant)]
    private static partial Regex ExpeditePattern();

    [GeneratedRegex("^SQ[0-7]{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex TransponderCodePattern();
}
