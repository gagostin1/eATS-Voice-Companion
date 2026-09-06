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

            VoiceInstructionType.MaintainSpeed =>
                EatsCommandFormatter.MaintainSpeed(
                    ParseNumber(value, "speed")),

            VoiceInstructionType.ProceedDirect =>
                EatsCommandFormatter.ProceedDirect(value!),

            VoiceInstructionType.Roger =>
                EatsCommandFormatter.Roger(),

            _ => throw new ArgumentOutOfRangeException(
                nameof(instructionType),
                "The instruction type is not supported.")
        };

        string transmission =
            EatsCommandFormatter.BuildTransmission(
                callsign,
                instruction);

        return EatsTransmissionValidator.Validate(
            transmission);
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
