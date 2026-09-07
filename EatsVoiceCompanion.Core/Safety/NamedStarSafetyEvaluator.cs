namespace EatsVoiceCompanion.Core.Safety;

public static class NamedStarSafetyEvaluator
{
    public static CommandSafetyResult Evaluate(
        string callsign,
        string? spokenStar,
        IReadOnlyDictionary<string, string> activeStars)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callsign);
        ArgumentNullException.ThrowIfNull(activeStars);

        if (!activeStars.TryGetValue(callsign, out string? assignedStar))
        {
            return new CommandSafetyResult(
                CommandSafetyState.PreviewOnly,
                "Preview only: a descend-via-capable STAR could not be " +
                $"verified for {callsign}.");
        }

        if (!string.IsNullOrWhiteSpace(spokenStar) &&
            !string.Equals(
                spokenStar,
                assignedStar,
                StringComparison.OrdinalIgnoreCase))
        {
            return new CommandSafetyResult(
                CommandSafetyState.Blocked,
                $"Blocked: spoken STAR {spokenStar} does not match " +
                $"{callsign}'s assigned STAR {assignedStar}.");
        }

        return new CommandSafetyResult(
            CommandSafetyState.Verified,
            $"Verified: {callsign} is assigned descend-via-capable " +
            $"STAR {assignedStar}.");
    }
}
