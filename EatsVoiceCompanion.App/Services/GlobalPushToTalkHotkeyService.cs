using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace EatsVoiceCompanion.App.Services;

public sealed class GlobalPushToTalkHotkeyService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private readonly LowLevelKeyboardProc _callback;
    private nint _hook;
    private bool _isPressed;
    private bool _disposed;

    public GlobalPushToTalkHotkeyService()
    {
        _callback = KeyboardHookCallback;
        _hook = SetWindowsHookEx(
            WhKeyboardLl,
            _callback,
            GetModuleHandle(null),
            0);

        if (_hook == nint.Zero)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The global push-to-talk keyboard listener could not start.");
        }
    }

    public PushToTalkHotkeyDefinition? Hotkey { get; set; }

    public event EventHandler? Pressed;

    public event EventHandler? Released;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_hook != nint.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = nint.Zero;
        }

        GC.SuppressFinalize(this);
    }

    private nint KeyboardHookCallback(
        int code,
        nint message,
        nint data)
    {
        if (code >= 0 && Hotkey is { } hotkey)
        {
            int virtualKey = Marshal.ReadInt32(data);
            int messageId = unchecked((int)message);

            if ((messageId is WmKeyDown or WmSysKeyDown) &&
                virtualKey == hotkey.VirtualKey &&
                CurrentModifiersExcluding(hotkey.Key) == hotkey.Modifiers)
            {
                if (!_isPressed)
                {
                    _isPressed = true;
                    Pressed?.Invoke(this, EventArgs.Empty);
                }
            }

            if ((messageId is WmKeyUp or WmSysKeyUp) &&
                virtualKey == hotkey.VirtualKey &&
                _isPressed)
            {
                _isPressed = false;
                Released?.Invoke(this, EventArgs.Empty);
            }
        }

        return CallNextHookEx(_hook, code, message, data);
    }

    private static ModifierKeys CurrentModifiers()
    {
        ModifierKeys result = ModifierKeys.None;

        if (IsKeyDown(0x11))
        {
            result |= ModifierKeys.Control;
        }

        if (IsKeyDown(0x12))
        {
            result |= ModifierKeys.Alt;
        }

        if (IsKeyDown(0x10))
        {
            result |= ModifierKeys.Shift;
        }

        if (IsKeyDown(0x5B) || IsKeyDown(0x5C))
        {
            result |= ModifierKeys.Windows;
        }

        return result;
    }

    private static ModifierKeys CurrentModifiersExcluding(Key mainKey)
    {
        return CurrentModifiers() & ~ModifierForKey(mainKey);
    }

    private static ModifierKeys ModifierForKey(Key key)
    {
        return key switch
        {
            Key.LeftCtrl or Key.RightCtrl => ModifierKeys.Control,
            Key.LeftAlt or Key.RightAlt => ModifierKeys.Alt,
            Key.LeftShift or Key.RightShift => ModifierKeys.Shift,
            Key.LWin or Key.RWin => ModifierKeys.Windows,
            _ => ModifierKeys.None
        };
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private delegate nint LowLevelKeyboardProc(
        int code,
        nint message,
        nint data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(
        int hookId,
        LowLevelKeyboardProc callback,
        nint module,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(
        nint hook,
        int code,
        nint message,
        nint data);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
