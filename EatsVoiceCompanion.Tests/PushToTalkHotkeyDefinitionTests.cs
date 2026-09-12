using System.Windows.Input;
using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.Tests;

public sealed class PushToTalkHotkeyDefinitionTests
{
    [Theory]
    [InlineData("F9", Key.F9, ModifierKeys.None)]
    [InlineData(
        "Ctrl+Shift+F9",
        Key.F9,
        ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData("Alt+Space", Key.Space, ModifierKeys.Alt)]
    [InlineData("RightCtrl", Key.RightCtrl, ModifierKeys.None)]
    [InlineData("LeftShift", Key.LeftShift, ModifierKeys.None)]
    public void TryParse_ReturnsCanonicalDefinition(
        string value,
        Key expectedKey,
        ModifierKeys expectedModifiers)
    {
        bool parsed = PushToTalkHotkeyDefinition.TryParse(
            value,
            out PushToTalkHotkeyDefinition? result);

        Assert.True(parsed);
        Assert.NotNull(result);
        Assert.Equal(expectedKey, result.Key);
        Assert.Equal(expectedModifiers, result.Modifiers);
        Assert.Equal(value, result.ToString());
    }

    [Theory]
    [InlineData("Ctrl")]
    [InlineData("Escape")]
    [InlineData("Ctrl+NotAKey")]
    [InlineData("Banana+F9")]
    [InlineData("999")]
    public void TryParse_RejectsInvalidAssignments(string value)
    {
        Assert.False(PushToTalkHotkeyDefinition.TryParse(value, out _));
    }

    [Fact]
    public void TryParse_TreatsEmptyValueAsUnassigned()
    {
        Assert.True(PushToTalkHotkeyDefinition.TryParse(null, out var result));
        Assert.Null(result);
    }
}
