using EatsVoiceCompanion.Core.Commands;

namespace EatsVoiceCompanion.App.Services;

public interface IEatsWindowInput
{
    bool IsWindowOwnedByProcess(nint windowHandle, int processId);

    bool Activate(nint windowHandle);

    bool IsForeground(nint windowHandle);

    void SendEscape();

    void SendEnter();

    void SendText(string text);
}

public sealed class EatsCommandStager
{
    private static readonly TimeSpan DefaultInputDelay =
        TimeSpan.FromMilliseconds(100);

    private readonly IEatsWindowInput _windowInput;
    private readonly TimeSpan _inputDelay;

    public EatsCommandStager(
        IEatsWindowInput? windowInput = null,
        TimeSpan? inputDelay = null)
    {
        _windowInput = windowInput ?? new WindowsEatsWindowInput();
        _inputDelay = inputDelay ?? DefaultInputDelay;

        if (_inputDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inputDelay),
                "The input delay cannot be negative.");
        }
    }

    public async Task StageAsync(
        EatsProcessInfo process,
        string transmission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(process);

        string validated = EatsTransmissionValidator.Validate(transmission);

        if (process.ProcessId <= 0 ||
            process.MainWindowHandle == IntPtr.Zero ||
            !_windowInput.IsWindowOwnedByProcess(
                process.MainWindowHandle,
                process.ProcessId))
        {
            throw new InvalidOperationException(
                "The detected eATS window is no longer valid.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_windowInput.Activate(process.MainWindowHandle))
        {
            throw new InvalidOperationException(
                "Windows could not bring the detected eATS window " +
                "to the foreground.");
        }

        await WaitForInputAsync(cancellationToken);
        EnsureForeground(process.MainWindowHandle);

        // Escape safely clears any incomplete entry. The first Enter moves
        // eATS from its data-block command box into the radio-command field.
        // A final Enter is intentionally never sent: transmission remains a
        // deliberate controller action after visual inspection in eATS.
        _windowInput.SendEscape();

        await WaitForInputAsync(cancellationToken);
        EnsureForeground(process.MainWindowHandle);
        _windowInput.SendEnter();

        await WaitForInputAsync(cancellationToken);
        EnsureForeground(process.MainWindowHandle);
        _windowInput.SendText(validated);
    }

    private Task WaitForInputAsync(CancellationToken cancellationToken)
    {
        return _inputDelay == TimeSpan.Zero
            ? Task.CompletedTask
            : Task.Delay(_inputDelay, cancellationToken);
    }

    private void EnsureForeground(nint expectedWindowHandle)
    {
        if (!_windowInput.IsForeground(expectedWindowHandle))
        {
            throw new InvalidOperationException(
                "eATS lost focus before command staging completed. " +
                "No further input was sent.");
        }
    }
}
