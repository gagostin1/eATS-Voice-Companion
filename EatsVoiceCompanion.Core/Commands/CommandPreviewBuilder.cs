using EatsVoiceCompanion.Core.Speech;

namespace EatsVoiceCompanion.Core.Commands;

public static class CommandPreviewBuilder
{
    public static string Build(
        string callsign,
        VoiceInstructionType instructionType,
        string? value)
    {
        string instruction = instructionType switch
        {
            VoiceInstructionType.FlyHeading =>
                EatsCommandFormatter.FlyHeading(
                    ParseNumber(value, "heading")),

            VoiceInstructionType.TurnLeftHeading =>
                EatsCommandFormatter.TurnLeftHeading(
                    ParseNumber(value, "heading")),

            VoiceInstructionType.TurnRightHeading =>
                EatsCommandFormatter.TurnRightHeading(
                    ParseNumber(value, "heading")),

            VoiceInstructionType.ClimbAndMaintain =>
                EatsCommandFormatter.ClimbAndMaintain(
                    ParseNumber(value, "altitude")),

            VoiceInstructionType.DescendAndMaintain =>
                EatsCommandFormatter.DescendAndMaintain(
                    ParseNumber(value, "altitude")),

            VoiceInstructionType.DescendVia =>
                EatsCommandFormatter.DescendVia(),

            VoiceInstructionType.DescendViaExceptMaintain =>
                EatsCommandFormatter.DescendViaExceptMaintain(
                    ParseNumber(value, "altitude")),

            VoiceInstructionType.DescendAtPilotsDiscretion =>
                EatsCommandFormatter.DescendAtPilotsDiscretion(
                    ParseNumber(value, "altitude")),

            VoiceInstructionType.Expedite =>
                EatsCommandFormatter.Expedite(),

            VoiceInstructionType.ExpediteThroughAltitude =>
                EatsCommandFormatter.ExpediteThroughAltitude(
                    ParseNumber(value, "altitude")),

            VoiceInstructionType.ReportLeavingAltitude =>
                EatsCommandFormatter.ReportLeavingAltitude(
                    ParseNumber(value, "altitude")),

            VoiceInstructionType.ReportReachingAltitude =>
                EatsCommandFormatter.ReportReachingAltitude(
                    ParseNumber(value, "altitude")),

            VoiceInstructionType.SayAltitude =>
                EatsCommandFormatter.SayAltitude(),

            VoiceInstructionType.MaintainSpeed =>
                EatsCommandFormatter.MaintainSpeed(
                    ParseNumber(value, "speed")),

            VoiceInstructionType.MaintainSpeedOrGreater =>
                EatsCommandFormatter.MaintainSpeedOrGreater(
                    ParseNumber(value, "speed")),

            VoiceInstructionType.MaintainSpeedOrLess =>
                EatsCommandFormatter.MaintainSpeedOrLess(
                    ParseNumber(value, "speed")),

            VoiceInstructionType.MaintainMach =>
                EatsCommandFormatter.MaintainMach(
                    ParseNumber(value, "Mach number")),

            VoiceInstructionType.MaintainMachOrGreater =>
                EatsCommandFormatter.MaintainMachOrGreater(
                    ParseNumber(value, "Mach number")),

            VoiceInstructionType.MaintainMachOrLess =>
                EatsCommandFormatter.MaintainMachOrLess(
                    ParseNumber(value, "Mach number")),

            VoiceInstructionType.ResumeNormalSpeed =>
                EatsCommandFormatter.ResumeNormalSpeed(),

            VoiceInstructionType.SayIndicatedSpeed =>
                EatsCommandFormatter.SayIndicatedSpeed(),

            VoiceInstructionType.SayMach =>
                EatsCommandFormatter.SayMach(),

            VoiceInstructionType.SayNormalSpeed =>
                EatsCommandFormatter.SayNormalSpeed(),

            VoiceInstructionType.FlyPresentHeading =>
                EatsCommandFormatter.FlyPresentHeading(),

            VoiceInstructionType.ExpectApproach =>
                EatsCommandFormatter.ExpectApproach(value!),

            VoiceInstructionType.InterceptFinalApproachCourse =>
                EatsCommandFormatter.InterceptFinalApproachCourse(),

            VoiceInstructionType.ClearedApproach =>
                EatsCommandFormatter.ClearedApproach(),

            VoiceInstructionType.SayApproachRequest =>
                EatsCommandFormatter.SayApproachRequest(),

            VoiceInstructionType.ReduceToFinalApproachSpeed =>
                EatsCommandFormatter.ReduceToFinalApproachSpeed(),

            VoiceInstructionType.ComplyWithPublishedSpeeds =>
                EatsCommandFormatter.ComplyWithPublishedSpeeds(value!),

            VoiceInstructionType.ProceedDirect =>
                EatsCommandFormatter.ProceedDirect(value!),

            VoiceInstructionType.CrossAtAltitude =>
                BuildCrossAtAltitude(value),

            VoiceInstructionType.CrossAtAltitudeAndSpeed =>
                BuildCrossAtAltitudeAndSpeed(value),

            VoiceInstructionType.Altimeter =>
                EatsCommandFormatter.Altimeter(
                    ParseNumber(value, "altimeter setting")),

            VoiceInstructionType.Roger =>
                EatsCommandFormatter.Roger(),

            _ => throw new ArgumentOutOfRangeException(
                nameof(instructionType),
                "The instruction type is not supported.")
        };

        string[] instructions = instructionType switch
        {
            VoiceInstructionType.ComplyWithPublishedSpeeds =>
                [EatsCommandFormatter.DescendVia(), instruction],
            VoiceInstructionType.InterceptFinalApproachCourse =>
                [EatsCommandFormatter.FlyPresentHeading(), instruction],
            VoiceInstructionType.ReduceToFinalApproachSpeed =>
                [EatsCommandFormatter.ClearedApproach(), instruction],
            _ => [instruction]
        };

        string transmission = EatsCommandFormatter.BuildTransmission(
            callsign,
            instructions);

        return EatsTransmissionValidator.Validate(
            transmission);
    }

    public static string BuildCombined(
        string callsign,
        string? instructionTokens)
    {
        if (string.IsNullOrWhiteSpace(instructionTokens))
        {
            throw new ArgumentException(
                "Enter at least one eATS instruction token.",
                nameof(instructionTokens));
        }

        string transmission = EatsCommandFormatter.BuildTransmission(
            callsign,
            instructionTokens.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries));

        return EatsTransmissionValidator.Validate(transmission);
    }

    private static string BuildCrossAtAltitude(string? value)
    {
        string[] values = SplitValues(value, 2, "fix and altitude");

        return EatsCommandFormatter.CrossAtAltitude(
            values[0],
            ParseNumber(values[1], "altitude"));
    }

    private static string BuildCrossAtAltitudeAndSpeed(string? value)
    {
        string[] values = SplitValues(
            value,
            3,
            "fix, altitude, and speed");

        return EatsCommandFormatter.CrossAtAltitudeAndSpeed(
            values[0],
            ParseNumber(values[1], "altitude"),
            ParseNumber(values[2], "speed"));
    }

    private static string[] SplitValues(
        string? value,
        int expectedCount,
        string description)
    {
        string[] values = value?.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

        if (values.Length != expectedCount)
        {
            throw new ArgumentException(
                $"Enter the {description}, separated by spaces.",
                nameof(value));
        }

        return values;
    }

    private static int ParseNumber(
        string? value,
        string valueName)
    {
        if (!int.TryParse(value, out int result))
        {
            throw new ArgumentException(
                $"Enter a valid numeric {valueName}.",
                nameof(value));
        }

        return result;
    }
}
