using System.Text.RegularExpressions;

namespace EatsVoiceCompanion.Core.Speech;

public sealed partial class VoiceCommandParser
{
    private static readonly CommandPattern[] ValuePatterns =
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
            VoiceInstructionType.ClimbAndMaintain,
            IsAltitude: true),

        new(
            "descend and maintain",
            VoiceInstructionType.DescendAndMaintain,
            IsAltitude: true),

        new(
            "descend via except maintain",
            VoiceInstructionType.DescendViaExceptMaintain,
            IsAltitude: true),

        new(
            "maintain speed",
            VoiceInstructionType.MaintainSpeed),

        new(
            "comply with published speed restrictions at",
            VoiceInstructionType.ComplyWithPublishedSpeeds,
            IsFix: true),

        new(
            "comply with speed restrictions at",
            VoiceInstructionType.ComplyWithPublishedSpeeds,
            IsFix: true),

        new(
            "comply with published speeds at",
            VoiceInstructionType.ComplyWithPublishedSpeeds,
            IsFix: true),

        new(
            "resume published speed at",
            VoiceInstructionType.ComplyWithPublishedSpeeds,
            IsFix: true),

        new(
            "proceed direct to",
            VoiceInstructionType.ProceedDirect,
            IsFix: true),

        new(
            "proceed direct",
            VoiceInstructionType.ProceedDirect,
            IsFix: true)
    };

    private static readonly string[] InstructionStartPhrases =
        ValuePatterns
            .Select(pattern => pattern.Phrase)
            .Concat(new[]
            {
                "descend via",
                "cross",
                "the",
                "welcome",
                "roger"
            })
            .OrderByDescending(phrase => phrase.Length)
            .ToArray();

    private readonly AirlineCallsignParser _callsignParser;

    public VoiceCommandParser(
        IReadOnlyDictionary<string, string> airlineAliases)
    {
        _callsignParser =
            new AirlineCallsignParser(airlineAliases);
    }

    public ParsedVoiceCommand Parse(
        string transcript,
        string? controllerPosition = null)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            throw new ArgumentException(
                "A transcript is required.",
                nameof(transcript));
        }

        string normalized = RemoveControllerPosition(
            NormalizeWords(transcript),
            controllerPosition);

        int instructionStart = FindInstructionStart(normalized);

        if (instructionStart <= 0)
        {
            throw new InvalidOperationException(
                "No supported instruction was recognized.");
        }

        string callsign = _callsignParser.Parse(
            normalized[..instructionStart].Trim());

        string instructionText =
            normalized[instructionStart..].Trim();

        IReadOnlyList<ParsedVoiceInstruction> instructions =
            ParseInstructionSequence(instructionText);

        return new ParsedVoiceCommand(callsign, instructions);
    }

    private static IReadOnlyList<ParsedVoiceInstruction>
        ParseInstructionSequence(string instructionText)
    {
        List<ParsedVoiceInstruction> instructions = new();
        string remaining = instructionText;

        while (remaining.Length > 0)
        {
            remaining = RemoveLeadingConnector(remaining);

            if (remaining is "welcome" or "roger")
            {
                instructions.Add(
                    new ParsedVoiceInstruction(
                        VoiceInstructionType.Roger));
                remaining = string.Empty;
                continue;
            }

            if (remaining.StartsWith(
                    "cross ",
                    StringComparison.Ordinal))
            {
                var result = ParseCrossingRestriction(remaining);
                instructions.Add(result.Instruction);
                remaining = result.Remaining;
                continue;
            }

            if (remaining.StartsWith(
                    "the ",
                    StringComparison.Ordinal))
            {
                var result = ParseAltimeter(remaining);
                instructions.Add(result.Instruction);
                remaining = result.Remaining;
                continue;
            }

            if (StartsWithPhrase(remaining, "descend via"))
            {
                var result = ParseDescendVia(remaining);
                instructions.Add(result.Instruction);
                remaining = result.Remaining;
                continue;
            }

            CommandPattern? pattern = ValuePatterns.FirstOrDefault(
                candidate => StartsWithPhrase(
                    remaining,
                    candidate.Phrase));

            if (pattern is not null)
            {
                string valueAndRest =
                    remaining[pattern.Phrase.Length..].Trim();

                var result = TakeValue(valueAndRest);

                instructions.Add(
                    ParseValueInstruction(pattern, result.Value));
                remaining = result.Remaining;
                continue;
            }

            throw new InvalidOperationException(
                $"The remaining instruction was not recognized: " +
                $"'{remaining}'.");
        }

        if (instructions.Count == 0)
        {
            throw new InvalidOperationException(
                "No supported instruction was recognized.");
        }

        return instructions;
    }

    private static (
        ParsedVoiceInstruction Instruction,
        string Remaining) ParseDescendVia(string value)
    {
        string remaining = value["descend via".Length..].Trim();

        if (remaining.Length == 0 ||
            (StartsWithInstructionOrConnector(remaining) &&
             !remaining.StartsWith("the ", StringComparison.Ordinal)))
        {
            return (
                new ParsedVoiceInstruction(
                    VoiceInstructionType.DescendVia),
                remaining);
        }

        const string exceptMaintain = "except maintain ";

        if (remaining.StartsWith(exceptMaintain, StringComparison.Ordinal))
        {
            var altitude = TakeValue(remaining[exceptMaintain.Length..]);

            return (
                new ParsedVoiceInstruction(
                    VoiceInstructionType.DescendViaExceptMaintain,
                    NumericValue:
                        AviationAltitudeParser.Parse(altitude.Value)),
                altitude.Remaining);
        }

        Match match = NamedStarPattern().Match(remaining);

        if (!match.Success)
        {
            throw new ArgumentException(
                "A named descend-via clearance must say the STAR name, " +
                "number, and 'arrival'.",
                nameof(value));
        }

        string star = StarNameNormalizer.Normalize(
            match.Groups["star"].Value);
        string afterArrival = match.Groups["remaining"].Value.Trim();

        if (afterArrival.StartsWith(exceptMaintain, StringComparison.Ordinal))
        {
            var altitude = TakeValue(
                afterArrival[exceptMaintain.Length..].Trim());

            return (
                new ParsedVoiceInstruction(
                    VoiceInstructionType.DescendViaExceptMaintain,
                    NumericValue:
                        AviationAltitudeParser.Parse(altitude.Value),
                    TextValue: star),
                altitude.Remaining);
        }

        if (afterArrival.Length > 0 &&
            !StartsWithInstructionOrConnector(afterArrival))
        {
            throw new InvalidOperationException(
                "The words after the named STAR were not recognized.");
        }

        return (
            new ParsedVoiceInstruction(
                VoiceInstructionType.DescendVia,
                TextValue: star),
            afterArrival);
    }

    private static ParsedVoiceInstruction ParseValueInstruction(
        CommandPattern pattern,
        string value)
    {
        if (value.Length == 0)
        {
            throw new ArgumentException(
                "The instruction value is missing.",
                nameof(value));
        }

        if (pattern.IsFix)
        {
            return new ParsedVoiceInstruction(
                pattern.InstructionType,
                TextValue: ParseFix(value));
        }

        if (pattern.InstructionType == VoiceInstructionType.MaintainSpeed &&
            value.EndsWith(" knots", StringComparison.Ordinal))
        {
            value = value[..^" knots".Length].Trim();
        }

        int numericValue = pattern.IsFlightLevel
            ? checked(AviationNumberParser.Parse(value) * 100)
            : pattern.IsAltitude
                ? AviationAltitudeParser.Parse(value)
                : AviationNumberParser.Parse(value);

        return new ParsedVoiceInstruction(
            pattern.InstructionType,
            NumericValue: numericValue);
    }

    private static (
        ParsedVoiceInstruction Instruction,
        string Remaining) ParseCrossingRestriction(string value)
    {
        string afterCross = value["cross ".Length..].Trim();
        int firstSpace = afterCross.IndexOf(' ');

        if (firstSpace <= 0)
        {
            throw new ArgumentException(
                "A crossing restriction requires a fix and altitude.",
                nameof(value));
        }

        string fix = ParseFix(afterCross[..firstSpace]);
        string restriction = afterCross[(firstSpace + 1)..].Trim();
        const string atAndMaintain = "at and maintain ";
        const string at = "at ";

        if (restriction.StartsWith(
                "at or above ",
                StringComparison.Ordinal) ||
            restriction.StartsWith(
                "at or below ",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "eATS does not provide an at-or-above or at-or-below " +
                "crossing command in the supplied radio reference.");
        }

        if (restriction.StartsWith(
                atAndMaintain,
                StringComparison.Ordinal))
        {
            restriction = restriction[atAndMaintain.Length..].Trim();
        }
        else if (restriction.StartsWith(at, StringComparison.Ordinal))
        {
            restriction = restriction[at.Length..].Trim();
        }
        else
        {
            throw new ArgumentException(
                "A crossing restriction must say 'at' or " +
                "'at and maintain'.",
                nameof(value));
        }

        int speedSeparator = restriction.IndexOf(
            " at ",
            StringComparison.Ordinal);

        if (speedSeparator >= 0)
        {
            string altitudeText = restriction[..speedSeparator].Trim();
            string speedAndRest = restriction[(speedSeparator + 4)..];
            int knotsEnd = speedAndRest.IndexOf(
                " knots",
                StringComparison.Ordinal);

            if (knotsEnd <= 0)
            {
                throw new ArgumentException(
                    "A combined crossing restriction must end its " +
                    "speed with 'knots'.",
                    nameof(value));
            }

            string speedText = speedAndRest[..knotsEnd].Trim();
            string remaining = speedAndRest[
                (knotsEnd + " knots".Length)..].Trim();

            return (
                new ParsedVoiceInstruction(
                    VoiceInstructionType.CrossAtAltitudeAndSpeed,
                    NumericValue: AviationAltitudeParser.Parse(altitudeText),
                    TextValue: fix,
                    SecondaryNumericValue:
                        AviationNumberParser.Parse(speedText)),
                remaining);
        }

        (string altitude, string remainingAfterAltitude) =
            TakeValue(restriction);

        return (
            new ParsedVoiceInstruction(
                VoiceInstructionType.CrossAtAltitude,
                NumericValue: AviationAltitudeParser.Parse(altitude),
                TextValue: fix),
            remainingAfterAltitude);
    }

    private static (
        ParsedVoiceInstruction Instruction,
        string Remaining) ParseAltimeter(string value)
    {
        string withoutThe = value["the ".Length..];
        int separator = withoutThe.IndexOf(
            " altimeter ",
            StringComparison.Ordinal);

        if (separator <= 0)
        {
            throw new ArgumentException(
                "An altimeter instruction requires a facility name.",
                nameof(value));
        }

        string facility = withoutThe[..separator].Trim();
        string settingAndRest = withoutThe[
            (separator + " altimeter ".Length)..].Trim();
        (string setting, string remaining) = TakeValue(settingAndRest);

        int numericSetting = AviationNumberParser.Parse(setting);

        if (numericSetting is < 1000 or > 9999)
        {
            throw new ArgumentException(
                "An altimeter setting must contain four digits.",
                nameof(value));
        }

        return (
            new ParsedVoiceInstruction(
                VoiceInstructionType.Altimeter,
                NumericValue: numericSetting,
                TextValue: facility),
            remaining);
    }

    private static (string Value, string Remaining) TakeValue(
        string valueAndRest)
    {
        if (valueAndRest.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        int boundary = FindNextInstructionBoundary(valueAndRest);

        if (boundary < 0)
        {
            return (valueAndRest.Trim(), string.Empty);
        }

        return (
            valueAndRest[..boundary].Trim(),
            valueAndRest[boundary..].Trim());
    }

    private static int FindNextInstructionBoundary(string value)
    {
        int earliest = -1;

        foreach (string connector in new[] { " then ", " and " })
        {
            int index = value.IndexOf(connector, StringComparison.Ordinal);

            if (index > 0 && (earliest < 0 || index < earliest))
            {
                earliest = index;
            }
        }

        foreach (string phrase in InstructionStartPhrases)
        {
            int index = value.IndexOf(
                " " + phrase,
                StringComparison.Ordinal);

            if (index > 0 && (earliest < 0 || index < earliest))
            {
                earliest = index;
            }
        }

        return earliest;
    }

    private static string RemoveLeadingConnector(string value)
    {
        foreach (string connector in new[] { "then ", "and " })
        {
            if (value.StartsWith(connector, StringComparison.Ordinal))
            {
                return value[connector.Length..].Trim();
            }
        }

        return value;
    }

    private static bool StartsWithInstructionOrConnector(string value)
    {
        if (value.StartsWith("then ", StringComparison.Ordinal) ||
            value.StartsWith("and ", StringComparison.Ordinal))
        {
            return true;
        }

        return InstructionStartPhrases.Any(
            phrase => StartsWithPhrase(value, phrase));
    }

    private static bool StartsWithPhrase(string value, string phrase)
    {
        return string.Equals(value, phrase, StringComparison.Ordinal) ||
               value.StartsWith(phrase + " ", StringComparison.Ordinal);
    }

    private static int FindInstructionStart(string value)
    {
        int earliest = -1;

        foreach (string phrase in InstructionStartPhrases)
        {
            int index = value.IndexOf(
                " " + phrase,
                StringComparison.Ordinal);

            if (index > 0 && (earliest < 0 || index + 1 < earliest))
            {
                earliest = index + 1;
            }
        }

        return earliest;
    }

    private static string ParseFix(string value)
    {
        if (value.Contains(' '))
        {
            throw new ArgumentException(
                "A fix must be recognized as one continuous word.",
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

    private static string RemoveControllerPosition(
        string normalizedTranscript,
        string? controllerPosition)
    {
        if (string.IsNullOrWhiteSpace(controllerPosition))
        {
            return normalizedTranscript;
        }

        string normalizedPosition = NormalizeWords(controllerPosition);

        if (normalizedPosition.Length == 0)
        {
            return normalizedTranscript;
        }

        string positionWithSpaces = $" {normalizedPosition} ";
        int positionIndex = normalizedTranscript.IndexOf(
            positionWithSpaces,
            StringComparison.OrdinalIgnoreCase);

        if (positionIndex <= 0)
        {
            return normalizedTranscript;
        }

        return normalizedTranscript
            .Remove(positionIndex, positionWithSpaces.Length)
            .Insert(positionIndex, " ");
    }

    private sealed record CommandPattern(
        string Phrase,
        VoiceInstructionType InstructionType,
        bool IsFlightLevel = false,
        bool IsAltitude = false,
        bool IsFix = false);

    [GeneratedRegex(
        "^(?:the )?(?<star>[a-z0-9]+(?: [a-z0-9]+)?) " +
        "arrival(?: (?<remaining>.*))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex NamedStarPattern();
}
