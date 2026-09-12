using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace EatsVoiceCompanion.App.Services;

public sealed class WindowsEatsWindowInput : IEatsWindowInput
{
    private const int RestoreWindow = 9;
    private const uint KeyboardInput = 1;
    private const uint KeyUp = 0x0002;
    private const uint Unicode = 0x0004;
    private const ushort EscapeKey = 0x1B;
    private const ushort EnterKey = 0x0D;
    private const ushort BackspaceKey = 0x08;
    private const ushort ControlKey = 0x11;
    private const ushort AKey = 0x41;

    public bool IsWindowOwnedByProcess(
        nint windowHandle,
        int processId)
    {
        if (windowHandle == IntPtr.Zero || !IsWindow(windowHandle))
        {
            return false;
        }

        _ = GetWindowThreadProcessId(
            windowHandle,
            out uint windowProcessId);

        return windowProcessId == (uint)processId;
    }

    public bool Activate(nint windowHandle)
    {
        if (IsIconic(windowHandle))
        {
            _ = ShowWindow(windowHandle, RestoreWindow);
        }

        return GetForegroundWindow() == windowHandle ||
               SetForegroundWindow(windowHandle);
    }

    public bool IsForeground(nint windowHandle)
    {
        return GetForegroundWindow() == windowHandle;
    }

    public bool FocusRadioCommandField(nint windowHandle)
    {
        List<ChildEditWindow> edits = [];

        _ = EnumChildWindows(
            windowHandle,
            (child, _) =>
            {
                StringBuilder className = new(capacity: 128);
                _ = GetClassName(child, className, className.Capacity);
                string value = className.ToString();

                if (IsWindowVisible(child) &&
                    IsWindowEnabled(child) &&
                    (value.Contains("edit", StringComparison.OrdinalIgnoreCase) ||
                     value.Contains(
                         "textbox",
                         StringComparison.OrdinalIgnoreCase)) &&
                    GetWindowRect(child, out WindowRectangle rectangle))
                {
                    edits.Add(new ChildEditWindow(child, rectangle));
                }

                return true;
            },
            IntPtr.Zero);

        ChildEditWindow? radioField = edits
            .OrderByDescending(edit => edit.Rectangle.Top)
            .ThenBy(edit => edit.Rectangle.Left)
            .FirstOrDefault();

        if (radioField is null)
        {
            return false;
        }

        uint currentThread = GetCurrentThreadId();
        uint targetThread = GetWindowThreadProcessId(
            radioField.Handle,
            out _);
        bool attached = currentThread != targetThread &&
            AttachThreadInput(currentThread, targetThread, true);

        try
        {
            _ = SetFocus(radioField.Handle);
            return GetFocus() == radioField.Handle;
        }
        finally
        {
            if (attached)
            {
                _ = AttachThreadInput(currentThread, targetThread, false);
            }
        }
    }

    public void SendEscape()
    {
        SendVirtualKey(EscapeKey);
    }

    public void ClearFocusedText()
    {
        Send(
        [
            CreateKeyboardInput(ControlKey, 0, 0),
            CreateKeyboardInput(AKey, 0, 0),
            CreateKeyboardInput(AKey, 0, KeyUp),
            CreateKeyboardInput(ControlKey, 0, KeyUp),
            CreateKeyboardInput(BackspaceKey, 0, 0),
            CreateKeyboardInput(BackspaceKey, 0, KeyUp)
        ]);
    }

    public void SendEnter()
    {
        SendVirtualKey(EnterKey);
    }

    public void SendText(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        Input[] inputs = text
            .SelectMany(CreateUnicodeKeyPress)
            .ToArray();

        Send(inputs);
    }

    private static IEnumerable<Input> CreateUnicodeKeyPress(char character)
    {
        yield return CreateKeyboardInput(
            virtualKey: 0,
            scanCode: character,
            flags: Unicode);
        yield return CreateKeyboardInput(
            virtualKey: 0,
            scanCode: character,
            flags: Unicode | KeyUp);
    }

    private static void SendVirtualKey(ushort virtualKey)
    {
        Send(
        [
            CreateKeyboardInput(virtualKey, 0, 0),
            CreateKeyboardInput(virtualKey, 0, KeyUp)
        ]);
    }

    private static Input CreateKeyboardInput(
        ushort virtualKey,
        ushort scanCode,
        uint flags)
    {
        return new Input
        {
            Type = KeyboardInput,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInputData
                {
                    VirtualKey = virtualKey,
                    ScanCode = scanCode,
                    Flags = flags
                }
            }
        };
    }

    private static void Send(Input[] inputs)
    {
        uint sent = SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<Input>());

        if (sent != inputs.Length)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Windows could not complete command staging input.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInputData Mouse;

        [FieldOffset(0)]
        public KeyboardInputData Keyboard;

        [FieldOffset(0)]
        public HardwareInputData Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInputData
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInputData
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private sealed record ChildEditWindow(
        nint Handle,
        WindowRectangle Rectangle);

    private delegate bool EnumChildProc(nint windowHandle, nint parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInputData
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint inputCount,
        Input[] inputs,
        int inputSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        nint windowHandle,
        out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(
        nint parentWindow,
        EnumChildProc callback,
        nint parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(
        nint windowHandle,
        StringBuilder className,
        int maximumCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(
        nint windowHandle,
        out WindowRectangle rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(nint windowHandle);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(
        uint sourceThread,
        uint targetThread,
        bool attach);

    [DllImport("user32.dll")]
    private static extern nint SetFocus(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern nint GetFocus();

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(
        nint windowHandle,
        int command);
}
