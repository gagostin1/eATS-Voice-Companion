namespace EatsVoiceCompanion.Core.Speech;

public sealed record ParsedVoiceInstruction(
    VoiceInstructionType InstructionType,
    int? NumericValue = null,
    string? TextValue = null,
    int? SecondaryNumericValue = null);
