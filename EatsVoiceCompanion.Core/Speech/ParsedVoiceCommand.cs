using EatsVoiceCompanion.Core.Commands;

namespace EatsVoiceCompanion.Core.Speech;

public sealed record ParsedVoiceCommand(
    string Callsign,
    VoiceInstructionType InstructionType,
    int? NumericValue = null,
    string? TextValue = null)
{
    public string ToEatsCommand()
    {
        string instruction = InstructionType switch
        {
            VoiceInstructionType.FlyHeading =>
                EatsCommandFormatter.FlyHeading(
                    RequireNumericValue()),

            VoiceInstructionType.TurnLeftHeading =>
                EatsCommandFormatter.TurnLeftHeading(
                    RequireNumericValue()),

            VoiceInstructionType.TurnRightHeading =>
                EatsCommandFormatter.TurnRightHeading(
                    RequireNumericValue()),

            VoiceInstructionType.ClimbAndMaintain =>
                EatsCommandFormatter.ClimbAndMaintain(
                    RequireNumericValue()),

            VoiceInstructionType.DescendAndMaintain =>
                EatsCommandFormatter.DescendAndMaintain(
                    RequireNumericValue()),

            VoiceInstructionType.MaintainSpeed =>
                EatsCommandFormatter.MaintainSpeed(
                    RequireNumericValue()),

            VoiceInstructionType.ProceedDirect =>
                EatsCommandFormatter.ProceedDirect(
                    RequireTextValue()),

            _ => throw new InvalidOperationException(
                "The instruction type is not supported.")
        };

        return EatsCommandFormatter.BuildTransmission(
            Callsign,
            instruction);
    }

    private int RequireNumericValue()
    {
        return NumericValue ??
            throw new InvalidOperationException(
                "This instruction requires a numeric value.");
    }

    private string RequireTextValue()
    {
        return TextValue ??
            throw new InvalidOperationException(
                "This instruction requires a text value.");
    }
}