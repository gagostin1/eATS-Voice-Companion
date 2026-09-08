using EatsVoiceCompanion.Core.Commands;

namespace EatsVoiceCompanion.Core.Speech;

public sealed record ParsedVoiceCommand
{
    public ParsedVoiceCommand(
        string callsign,
        IReadOnlyList<ParsedVoiceInstruction> instructions)
    {
        if (string.IsNullOrWhiteSpace(callsign))
        {
            throw new ArgumentException(
                "A callsign is required.",
                nameof(callsign));
        }

        if (instructions is null || instructions.Count == 0)
        {
            throw new ArgumentException(
                "At least one instruction is required.",
                nameof(instructions));
        }

        Callsign = callsign;
        Instructions = instructions.ToArray();
    }

    public ParsedVoiceCommand(
        string callsign,
        VoiceInstructionType instructionType,
        int? numericValue = null,
        string? textValue = null)
        : this(
            callsign,
            new[]
            {
                new ParsedVoiceInstruction(
                    instructionType,
                    numericValue,
                    textValue)
            })
    {
    }

    public string Callsign { get; }

    public IReadOnlyList<ParsedVoiceInstruction> Instructions { get; }

    public VoiceInstructionType InstructionType =>
        RequireSingleInstruction().InstructionType;

    public int? NumericValue => RequireSingleInstruction().NumericValue;

    public string? TextValue => RequireSingleInstruction().TextValue;

    public IReadOnlyList<string> ToEatsInstructionTokens()
    {
        return Instructions
            .Select(ToEatsInstructionToken)
            .ToArray();
    }

    public string ToEatsCommand()
    {
        return EatsCommandFormatter.BuildTransmission(
            Callsign,
            ToEatsInstructionTokens().ToArray());
    }

    private static string ToEatsInstructionToken(
        ParsedVoiceInstruction instruction)
    {
        return instruction.InstructionType switch
        {
            VoiceInstructionType.FlyHeading =>
                EatsCommandFormatter.FlyHeading(
                    RequireNumericValue(instruction)),

            VoiceInstructionType.TurnLeftHeading =>
                EatsCommandFormatter.TurnLeftHeading(
                    RequireNumericValue(instruction)),

            VoiceInstructionType.TurnRightHeading =>
                EatsCommandFormatter.TurnRightHeading(
                    RequireNumericValue(instruction)),

            VoiceInstructionType.ClimbAndMaintain =>
                EatsCommandFormatter.ClimbAndMaintain(
                    RequireNumericValue(instruction)),

            VoiceInstructionType.DescendAndMaintain =>
                EatsCommandFormatter.DescendAndMaintain(
                    RequireNumericValue(instruction)),

            VoiceInstructionType.DescendVia =>
                EatsCommandFormatter.DescendVia(),

            VoiceInstructionType.DescendViaExceptMaintain =>
                EatsCommandFormatter.DescendViaExceptMaintain(
                    RequireNumericValue(instruction)),

            VoiceInstructionType.MaintainSpeed =>
                EatsCommandFormatter.MaintainSpeed(
                    RequireNumericValue(instruction)),

            VoiceInstructionType.ComplyWithPublishedSpeeds =>
                EatsCommandFormatter.ComplyWithPublishedSpeeds(
                    RequireTextValue(instruction)),

            VoiceInstructionType.ProceedDirect =>
                EatsCommandFormatter.ProceedDirect(
                    RequireTextValue(instruction)),

            VoiceInstructionType.CrossAtAltitude =>
                EatsCommandFormatter.CrossAtAltitude(
                    RequireTextValue(instruction),
                    RequireNumericValue(instruction)),

            VoiceInstructionType.CrossAtAltitudeAndSpeed =>
                EatsCommandFormatter.CrossAtAltitudeAndSpeed(
                    RequireTextValue(instruction),
                    RequireNumericValue(instruction),
                    RequireSecondaryNumericValue(instruction)),

            VoiceInstructionType.Altimeter =>
                EatsCommandFormatter.Altimeter(
                    RequireNumericValue(instruction)),

            VoiceInstructionType.Roger =>
                EatsCommandFormatter.Roger(),

            _ => throw new InvalidOperationException(
                "The instruction type is not supported.")
        };
    }

    private ParsedVoiceInstruction RequireSingleInstruction()
    {
        if (Instructions.Count != 1)
        {
            throw new InvalidOperationException(
                "This property is only available for a single instruction.");
        }

        return Instructions[0];
    }

    private static int RequireNumericValue(
        ParsedVoiceInstruction instruction)
    {
        return instruction.NumericValue ??
            throw new InvalidOperationException(
                "This instruction requires a numeric value.");
    }

    private static int RequireSecondaryNumericValue(
        ParsedVoiceInstruction instruction)
    {
        return instruction.SecondaryNumericValue ??
            throw new InvalidOperationException(
                "This instruction requires a second numeric value.");
    }

    private static string RequireTextValue(
        ParsedVoiceInstruction instruction)
    {
        return instruction.TextValue ??
            throw new InvalidOperationException(
                "This instruction requires a text value.");
    }
}
