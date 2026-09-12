using EatsVoiceCompanion.App.Services;

namespace EatsVoiceCompanion.Tests;

public sealed class EatsCommandStagerTests
{
    private static readonly EatsProcessInfo Process = new(
        ProcessId: 123,
        WindowTitle: "eATS",
        MainWindowHandle: 456,
        ExecutablePath: null,
        ProductVersion: null);

    [Fact]
    public async Task StageAsync_ClearsSelectsAndTypesWithoutTransmitting()
    {
        FakeWindowInput input = new();
        EatsCommandStager stager = CreateStager(input);

        await stager.StageAsync(Process, "aal123 fh270");

        Assert.Equal(
        [
            "OwnsWindow",
            "Activate",
            "Foreground",
            "FocusRadio",
            "Foreground",
            "ClearFocusedText",
            "Foreground",
            "Text:AAL123 FH270"
        ],
            input.Calls);
        Assert.DoesNotContain("Enter", input.Calls);
        Assert.DoesNotContain("Escape", input.Calls);
    }

    [Fact]
    public async Task StageAsync_RejectsInvalidTransmissionBeforeWindowInput()
    {
        FakeWindowInput input = new();
        EatsCommandStager stager = CreateStager(input);

        await Assert.ThrowsAsync<ArgumentException>(
            () => stager.StageAsync(Process, "AAL123 DELETE"));

        Assert.Empty(input.Calls);
    }

    [Fact]
    public async Task StageAsync_RejectsWindowNotOwnedByDetectedProcess()
    {
        FakeWindowInput input = new()
        {
            OwnsWindow = false
        };
        EatsCommandStager stager = CreateStager(input);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => stager.StageAsync(Process, "AAL123 FH270"));

        Assert.Contains("no longer valid", exception.Message);
        Assert.Equal(["OwnsWindow"], input.Calls);
    }

    [Fact]
    public async Task StageAsync_StopsWhenWindowCannotBeActivated()
    {
        FakeWindowInput input = new()
        {
            ActivationSucceeds = false
        };
        EatsCommandStager stager = CreateStager(input);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => stager.StageAsync(Process, "AAL123 FH270"));

        Assert.Contains("foreground", exception.Message);
        Assert.Equal(["OwnsWindow", "Activate"], input.Calls);
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, false, false)]
    [InlineData(2, false, false)]
    public async Task StageAsync_StopsIfEatsLosesFocus(
        int successfulForegroundChecks,
        bool expectedEscape,
        bool expectedEnter)
    {
        FakeWindowInput input = new()
        {
            SuccessfulForegroundChecks = successfulForegroundChecks
        };
        EatsCommandStager stager = CreateStager(input);

        InvalidOperationException exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => stager.StageAsync(Process, "AAL123 FH270"));

        Assert.Contains("lost focus", exception.Message);
        Assert.Equal(expectedEscape, input.Calls.Contains("Escape"));
        Assert.Equal(expectedEnter, input.Calls.Contains("Enter"));
        Assert.DoesNotContain(
            input.Calls,
            call => call.StartsWith("Text:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StageAsync_UsesEnterWhenRadioFieldIsNotExposed()
    {
        FakeWindowInput input = new()
        {
            RadioFocusSucceeds = false
        };
        EatsCommandStager stager = CreateStager(input);

        await stager.StageAsync(Process, "AAL123 FH270");

        Assert.Contains("FocusRadio", input.Calls);
        Assert.Contains("Enter", input.Calls);
        Assert.Equal("Text:AAL123 FH270", input.Calls[^1]);
    }

    [Fact]
    public void Constructor_RejectsNegativeInputDelay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new EatsCommandStager(
                new FakeWindowInput(),
                TimeSpan.FromMilliseconds(-1)));
    }

    private static EatsCommandStager CreateStager(FakeWindowInput input)
    {
        return new EatsCommandStager(input, TimeSpan.Zero);
    }

    private sealed class FakeWindowInput : IEatsWindowInput
    {
        private int _foregroundChecks;

        public List<string> Calls { get; } = [];

        public bool OwnsWindow { get; init; } = true;

        public bool ActivationSucceeds { get; init; } = true;

        public int SuccessfulForegroundChecks { get; init; } = int.MaxValue;

        public bool RadioFocusSucceeds { get; init; } = true;

        public bool IsWindowOwnedByProcess(
            nint windowHandle,
            int processId)
        {
            Calls.Add("OwnsWindow");
            return OwnsWindow;
        }

        public bool Activate(nint windowHandle)
        {
            Calls.Add("Activate");
            return ActivationSucceeds;
        }

        public bool IsForeground(nint windowHandle)
        {
            Calls.Add("Foreground");
            return _foregroundChecks++ < SuccessfulForegroundChecks;
        }

        public void SendEscape()
        {
            Calls.Add("Escape");
        }

        public void ClearFocusedText()
        {
            Calls.Add("ClearFocusedText");
        }

        public bool FocusRadioCommandField(nint windowHandle)
        {
            Calls.Add("FocusRadio");
            return RadioFocusSucceeds;
        }

        public void SendEnter()
        {
            Calls.Add("Enter");
        }

        public void SendText(string text)
        {
            Calls.Add("Text:" + text);
        }
    }
}
