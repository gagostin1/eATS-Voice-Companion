namespace EatsVoiceCompanion.Core.Safety;

public enum CommandSafetyState
{
    NotReady,
    PreviewOnly,
    Verified,
    Blocked
}

public sealed record CommandSafetyResult(
    CommandSafetyState State,
    string Message);

public static class CommandSafetyEvaluator
{
    public static CommandSafetyResult Evaluate(
        string callsign,
        bool hasFreshSnapshot,
        IEnumerable<string> activeCallsigns)
    {
        if (!hasFreshSnapshot)
        {
            return new CommandSafetyResult(
                CommandSafetyState.PreviewOnly,
                "Preview only: the callsign could not be " +
                "verified against a fresh eATS snapshot.");
        }

        bool isActive = ActiveCallsignValidator.IsActive(
            callsign,
            activeCallsigns);

        return isActive
            ? new CommandSafetyResult(
                CommandSafetyState.Verified,
                $"Verified: {callsign} is active in the " +
                "current eATS snapshot.")
            : new CommandSafetyResult(
                CommandSafetyState.Blocked,
                $"Blocked: {callsign} was not found in the " +
                "current eATS snapshot.");
    }

    public static CommandSafetyResult NotReady(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "A reason is required.",
                nameof(reason));
        }

        return new CommandSafetyResult(
            CommandSafetyState.NotReady,
            $"Not ready: {reason}");
    }
}
