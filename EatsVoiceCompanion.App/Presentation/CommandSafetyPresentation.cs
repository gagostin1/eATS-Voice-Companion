using System.Windows.Media;
using EatsVoiceCompanion.Core.Safety;

namespace EatsVoiceCompanion.App.Presentation;

public sealed record CommandSafetyPresentation(
    Brush Foreground,
    string Message);

public static class CommandSafetyPresenter
{
    public static CommandSafetyPresentation Create(
        CommandSafetyResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Brush foreground = result.State switch
        {
            CommandSafetyState.Verified => Brushes.ForestGreen,
            CommandSafetyState.PreviewOnly => Brushes.DarkGoldenrod,
            CommandSafetyState.Blocked or CommandSafetyState.NotReady =>
                Brushes.Firebrick,
            _ => throw new ArgumentOutOfRangeException(
                nameof(result),
                "The safety state is not supported.")
        };

        return new CommandSafetyPresentation(
            foreground,
            result.Message);
    }
}
