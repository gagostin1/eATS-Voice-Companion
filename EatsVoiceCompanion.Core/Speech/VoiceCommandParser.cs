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
            "descend at pilots discretion maintain flight level",
            VoiceInstructionType.DescendAtPilotsDiscretion,
            IsFlightLevel: true),

        new(
            "descend at pilots discretion to flight level",
            VoiceInstructionType.DescendAtPilotsDiscretion,
            IsFlightLevel: true),

        new(
            "descend at pilots discretion maintain",
            VoiceInstructionType.DescendAtPilotsDiscretion,
            IsAltitude: true),

        new(
            "descend at pilots discretion to",
            VoiceInstructionType.DescendAtPilotsDiscretion,
            IsAltitude: true),

        new(
            "report leaving flight level",
            VoiceInstructionType.ReportLeavingAltitude,
            IsFlightLevel: true),

        new(
            "report reaching flight level",
            VoiceInstructionType.ReportReachingAltitude,
            IsFlightLevel: true),

        new(
            "report leaving",
            VoiceInstructionType.ReportLeavingAltitude,
            IsAltitude: true),

        new(
            "report reaching",
            VoiceInstructionType.ReportReachingAltitude,
            IsAltitude: true),

        new(
            "maintain speed",
            VoiceInstructionType.MaintainSpeed),

        new(
            "maintain mach",
            VoiceInstructionType.MaintainMach),

        new(
            "maintain",
            VoiceInstructionType.MaintainSpeed),

        new(
            "expect approach",
            VoiceInstructionType.ExpectApproach,
            IsApproachId: true),

        new(
            "expect",
            VoiceInstructionType.ExpectApproach,
            IsApproachId: true),

        new(
            "contact frequency",
            VoiceInstructionType.ContactFrequency,
            IsFrequency: true),

        new(
            "contact",
            VoiceInstructionType.ContactFrequency,
            IsFrequency: true),

        new(
            "squawk",
            VoiceInstructionType.SquawkCode,
            IsTransponderCode: true),

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
                "say normal speed and mach",
                "say normal speed or mach",
                "say normal speed",
                "say indicated speed",
                "say airspeed",
                "say mach number",
                "say mach",
                "resume normal speed",
                "intercept the final approach course",
                "intercept final approach course",
                "cleared for the approach",
                "cleared for approach",
                "cleared approach",
                "fly present heading",
                "say approach request",
                "reduce speed to final approach speed",
                "reduce to final approach speed",
                "remain this frequency",
                "say again",
                "stand by",
                "standby",
                "stop altitude squawk",
                "squawk altitude",
                "squawk normal",
                "squawk standby",
                "squawk vfr",
                "squawk ident",
                "ident",
                "descend via",
                "expedite",
                "say altitude",
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

            if (StartsWithPhrase(remaining, "say altitude"))
            {
                string afterSayAltitude =
                    remaining["say altitude".Length..].Trim();

                if (afterSayAltitude.Length > 0 &&
                    !StartsWithInstructionOrConnector(afterSayAltitude))
                {
                    throw new InvalidOperationException(
                        "The words after 'say altitude' were not recognized.");
                }

                instructions.Add(
                    new ParsedVoiceInstruction(
                        VoiceInstructionType.SayAltitude));
                remaining = afterSayAltitude;
                continue;
            }

            if (TryParseNoValueInstruction(
                    remaining,
                    out ParsedVoiceInstruction? noValueInstruction,
                    out string afterNoValue))
            {
                instructions.Add(noValueInstruction);
                remaining = afterNoValue;
                continue;
            }

            if (StartsWithPhrase(remaining, "expedite"))
            {
                var result = ParseExpedite(remaining);
                instructions.Add(result.Instruction);
                remaining = result.Remaining;
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
        string Remaining) ParseExpedite(string value)
    {
        string remaining = value["expedite".Length..].Trim();

        if (remaining.Length == 0 ||
            StartsWithInstructionOrConnector(remaining))
        {
            return (
                new ParsedVoiceInstruction(
                    VoiceInstructionType.Expedite),
                remaining);
        }

        foreach (string direction in new[]
                 {
                     "climb ",
                     "descent ",
                     "descend "
                 })
        {
            if (remaining.StartsWith(direction, StringComparison.Ordinal))
            {
                remaining = remaining[direction.Length..].Trim();
                break;
            }
        }

        string altitudeText;

        if (remaining.StartsWith("through ", StringComparison.Ordinal))
        {
            altitudeText = remaining["through ".Length..].Trim();
        }
        else if (remaining.StartsWith("to ", StringComparison.Ordinal))
        {
            altitudeText = remaining["to ".Length..].Trim();
        }
        else
        {
            throw new ArgumentException(
                "An expedite altitude must say 'through' or 'to'.",
                nameof(value));
        }

        (string altitude, string afterAltitude) = TakeValue(altitudeText);
        bool isFlightLevel = altitude.StartsWith(
            "flight level ",
            StringComparison.Ordinal);
        string numericAltitude = isFlightLevel
            ? altitude["flight level ".Length..].Trim()
            : altitude;
        int altitudeFeet = isFlightLevel
            ? checked(AviationNumberParser.Parse(numericAltitude) * 100)
            : AviationAltitudeParser.Parse(numericAltitude);

        return (
            new ParsedVoiceInstruction(
                VoiceInstructionType.ExpediteThroughAltitude,
                NumericValue: altitudeFeet),
            afterAltitude);
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

        if (pattern.IsApproachId)
        {
            return new ParsedVoiceInstruction(
                pattern.InstructionType,
                TextValue: ApproachIdNormalizer.Normalize(value));
        }

        if (pattern.IsFrequency)
        {
            string frequency = ExtractFrequencyText(value);
            AviationFrequencyParser.ParseHundredths(frequency);

            return new ParsedVoiceInstruction(
                pattern.InstructionType,
                TextValue: frequency);
        }

        if (pattern.IsTransponderCode)
        {
            return new ParsedVoiceInstruction(
                pattern.InstructionType,
                TextValue: TransponderCodeParser.Parse(value));
        }

        if (pattern.InstructionType == VoiceInstructionType.MaintainSpeed)
        {
            VoiceInstructionType type = ParseLimitSuffix(
                ref value,
                VoiceInstructionType.MaintainSpeed,
                VoiceInstructionType.MaintainSpeedOrGreater,
                VoiceInstructionType.MaintainSpeedOrLess);

            bool saysKnots = value.EndsWith(" knots", StringComparison.Ordinal);

            if (saysKnots)
            {
                value = value[..^" knots".Length].Trim();
            }

            if (pattern.Phrase == "maintain" && !saysKnots)
            {
                throw new InvalidOperationException(
                    "A standalone speed assignment must include 'knots'.");
            }

            return new ParsedVoiceInstruction(
                type,
                NumericValue: AviationNumberParser.Parse(value));
        }

        if (pattern.InstructionType == VoiceInstructionType.MaintainMach)
        {
            VoiceInstructionType type = ParseLimitSuffix(
                ref value,
                VoiceInstructionType.MaintainMach,
                VoiceInstructionType.MaintainMachOrGreater,
                VoiceInstructionType.MaintainMachOrLess);

            foreach (string prefix in new[] { "zero point ", "point ", "decimal " })
            {
                if (value.StartsWith(prefix, StringComparison.Ordinal))
                {
                    value = value[prefix.Length..].Trim();
                    break;
                }
            }

            return new ParsedVoiceInstruction(
                type,
                NumericValue: AviationNumberParser.Parse(value));
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

    private static VoiceInstructionType ParseLimitSuffix(
        ref string value,
        VoiceInstructionType exact,
        VoiceInstructionType orGreater,
        VoiceInstructionType orLess)
    {
        if (value.EndsWith(" or greater", StringComparison.Ordinal))
        {
            value = value[..^" or greater".Length].Trim();
            return orGreater;
        }

        if (value.EndsWith(" or less", StringComparison.Ordinal))
        {
            value = value[..^" or less".Length].Trim();
            return orLess;
        }

        return exact;
    }

    private static string ExtractFrequencyText(string value)
    {
        int onIndex = value.LastIndexOf(" on ", StringComparison.Ordinal);

        if (onIndex >= 0)
        {
            return value[(onIndex + " on ".Length)..].Trim();
        }

        string[] tokens = value.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries);
        int firstDigit = Array.FindIndex(
            tokens,
            AviationFrequencyParser.IsDigitToken);

        if (firstDigit < 0)
        {
            throw new InvalidOperationException(
                "A contact instruction requires a spoken frequency.");
        }

        return string.Join(' ', tokens.Skip(firstDigit));
    }

    private static bool TryParseNoValueInstruction(
        string remaining,
        out ParsedVoiceInstruction instruction,
        out string afterInstruction)
    {
        (string Phrase, VoiceInstructionType Type)[] patterns =
        [
            ("say normal speed and mach", VoiceInstructionType.SayNormalSpeed),
            ("say normal speed or mach", VoiceInstructionType.SayNormalSpeed),
            ("say normal speed", VoiceInstructionType.SayNormalSpeed),
            ("say indicated speed", VoiceInstructionType.SayIndicatedSpeed),
            ("say airspeed", VoiceInstructionType.SayIndicatedSpeed),
            ("say mach number", VoiceInstructionType.SayMach),
            ("say mach", VoiceInstructionType.SayMach),
            ("resume normal speed", VoiceInstructionType.ResumeNormalSpeed),
            ("intercept the final approach course", VoiceInstructionType.InterceptFinalApproachCourse),
            ("intercept final approach course", VoiceInstructionType.InterceptFinalApproachCourse),
            ("cleared for the approach", VoiceInstructionType.ClearedApproach),
            ("cleared for approach", VoiceInstructionType.ClearedApproach),
            ("cleared approach", VoiceInstructionType.ClearedApproach),
            ("fly present heading", VoiceInstructionType.FlyPresentHeading),
            ("say approach request", VoiceInstructionType.SayApproachRequest),
            ("reduce speed to final approach speed", VoiceInstructionType.ReduceToFinalApproachSpeed),
            ("reduce to final approach speed", VoiceInstructionType.ReduceToFinalApproachSpeed),
            ("remain this frequency", VoiceInstructionType.RemainThisFrequency),
            ("say again", VoiceInstructionType.SayAgain),
            ("stand by", VoiceInstructionType.StandBy),
            ("standby", VoiceInstructionType.StandBy),
            ("stop altitude squawk", VoiceInstructionType.StopAltitudeSquawk),
            ("squawk altitude", VoiceInstructionType.SquawkAltitude),
            ("squawk normal", VoiceInstructionType.SquawkNormal),
            ("squawk standby", VoiceInstructionType.SquawkStandby),
            ("squawk vfr", VoiceInstructionType.SquawkVfr),
            ("squawk ident", VoiceInstructionType.SquawkIdent),
            ("ident", VoiceInstructionType.SquawkIdent)
        ];

        foreach (var pattern in patterns)
        {
            if (!StartsWithPhrase(remaining, pattern.Phrase))
            {
                continue;
            }

            instruction = new ParsedVoiceInstruction(pattern.Type);
            afterInstruction = remaining[pattern.Phrase.Length..].Trim();
            return true;
        }

        instruction = null!;
        afterInstruction = remaining;
        return false;
    }

    private static (
        ParsedVoiceInstruction Instruction,
        string Remaining) ParseCrossingRestriction(string value)
    {
        string afterCross = value["cross ".Length..].Trim();

        if (ContainsDistanceSeparator(afterCross))
        {
            return ParseCrossDistanceRestriction(afterCross, value);
        }

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
                    NumericValue: ParseCrossingAltitude(altitudeText),
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
                NumericValue: ParseCrossingAltitude(altitude),
                TextValue: fix),
            remainingAfterAltitude);
    }

    private static bool ContainsDistanceSeparator(string value) =>
        value.Contains(" mile ", StringComparison.Ordinal) ||
        value.Contains(" miles ", StringComparison.Ordinal);

    private static (
        ParsedVoiceInstruction Instruction,
        string Remaining) ParseCrossDistanceRestriction(
            string afterCross,
            string originalValue)
    {
        int distanceEnd = afterCross.IndexOf(
            " miles ",
            StringComparison.Ordinal);
        int separatorLength = " miles ".Length;

        if (distanceEnd < 0)
        {
            distanceEnd = afterCross.IndexOf(
                " mile ",
                StringComparison.Ordinal);
            separatorLength = " mile ".Length;
        }

        string distanceText = afterCross[..distanceEnd].Trim();
        string afterDistance = afterCross[
            (distanceEnd + separatorLength)..].Trim();
        int ofSeparator = afterDistance.IndexOf(
            " of ",
            StringComparison.Ordinal);

        if (ofSeparator <= 0)
        {
            throw new ArgumentException(
                "A cross-distance restriction must include a compass " +
                "direction followed by 'of'.",
                nameof(originalValue));
        }

        string direction = CrossDirectionNormalizer.Normalize(
            afterDistance[..ofSeparator]);
        string fixAndRestriction = afterDistance[
            (ofSeparator + " of ".Length)..].Trim();
        int restrictionSeparator = fixAndRestriction.IndexOf(
            " at and maintain ",
            StringComparison.Ordinal);
        int restrictionSeparatorLength = " at and maintain ".Length;

        if (restrictionSeparator <= 0)
        {
            restrictionSeparator = fixAndRestriction.IndexOf(
                " at ",
                StringComparison.Ordinal);
            restrictionSeparatorLength = " at ".Length;
        }

        if (restrictionSeparator <= 0)
        {
            throw new ArgumentException(
                "A cross-distance restriction requires a fix and altitude.",
                nameof(originalValue));
        }

        string fix = ParseFix(
            fixAndRestriction[..restrictionSeparator].Trim());
        string altitudeAndRest = fixAndRestriction[
            (restrictionSeparator + restrictionSeparatorLength)..].Trim();
        (string altitude, string remaining) = TakeValue(altitudeAndRest);

        return (
            new ParsedVoiceInstruction(
                VoiceInstructionType.CrossDistanceAtAltitude,
                NumericValue: ParseCrossingAltitude(altitude),
                TextValue: fix,
                SecondaryNumericValue:
                    AviationDistanceParser.Parse(distanceText),
                SecondaryTextValue: direction),
            remaining);
    }

    private static int ParseCrossingAltitude(string value)
    {
        const string flightLevel = "flight level ";

        return value.StartsWith(flightLevel, StringComparison.Ordinal)
            ? checked(
                AviationNumberParser.Parse(value[flightLevel.Length..]) *
                100)
            : AviationAltitudeParser.Parse(value);
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

        normalized = Regex.Replace(
            normalized,
            @"\s+",
            " ").Trim();

        return Regex.Replace(
            normalized,
            @"\bpilot s discretion\b",
            "pilots discretion");
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
        bool IsFix = false,
        bool IsApproachId = false,
        bool IsFrequency = false,
        bool IsTransponderCode = false);

    [GeneratedRegex(
        "^(?:the )?(?<star>[a-z0-9]+(?: [a-z0-9]+)?) " +
        "arrival(?: (?<remaining>.*))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex NamedStarPattern();
}
