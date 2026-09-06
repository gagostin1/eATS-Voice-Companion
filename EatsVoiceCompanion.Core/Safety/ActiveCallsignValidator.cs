namespace EatsVoiceCompanion.Core.Safety;

public static class ActiveCallsignValidator
{
    public static bool IsActive(
        string callsign,
        IEnumerable<string> activeCallsigns)
    {
        if (string.IsNullOrWhiteSpace(callsign))
        {
            throw new ArgumentException(
                "A callsign is required.",
                nameof(callsign));
        }

        ArgumentNullException.ThrowIfNull(
            activeCallsigns);

        string normalizedCallsign =
            callsign.Trim().ToUpperInvariant();

        return activeCallsigns.Any(
            activeCallsign =>
                !string.IsNullOrWhiteSpace(activeCallsign) &&
                string.Equals(
                    activeCallsign.Trim(),
                    normalizedCallsign,
                    StringComparison.OrdinalIgnoreCase));
    }
}