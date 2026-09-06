using System.Windows.Media;
using EatsVoiceCompanion.App.Presentation;
using EatsVoiceCompanion.Core.Safety;

namespace EatsVoiceCompanion.Tests;

public sealed class CommandSafetyPresenterTests
{
    [Theory]
    [InlineData(CommandSafetyState.Verified, "ForestGreen")]
    [InlineData(CommandSafetyState.PreviewOnly, "DarkGoldenrod")]
    [InlineData(CommandSafetyState.Blocked, "Firebrick")]
    [InlineData(CommandSafetyState.NotReady, "Firebrick")]
    public void Create_MapsSafetyStateToExpectedUiColor(
        CommandSafetyState state,
        string expectedColor)
    {
        CommandSafetyResult result = new(state, "Status message");

        CommandSafetyPresentation presentation =
            CommandSafetyPresenter.Create(result);

        SolidColorBrush brush =
            Assert.IsType<SolidColorBrush>(presentation.Foreground);

        Assert.Equal(
            ((SolidColorBrush)new BrushConverter()
                .ConvertFromString(expectedColor)!).Color,
            brush.Color);
        Assert.Equal("Status message", presentation.Message);
    }
}
