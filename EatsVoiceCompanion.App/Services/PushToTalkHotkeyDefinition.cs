using System.Windows.Input;

namespace EatsVoiceCompanion.App.Services;

public sealed record PushToTalkHotkeyDefinition(
    Key Key,
    ModifierKeys Modifiers)
{
    public int VirtualKey => KeyInterop.VirtualKeyFromKey(Key);

    public static PushToTalkHotkeyDefinition Create(
        Key key,
        ModifierKeys modifiers)
    {
        if (!Enum.IsDefined(key) ||
            KeyInterop.VirtualKeyFromKey(key) == 0 ||
            key is Key.None or Key.Escape)
        {
            throw new ArgumentException(
                "Choose a keyboard key other than Escape.",
                nameof(key));
        }

        ModifierKeys supported = ModifierKeys.Control |
                                 ModifierKeys.Alt |
                                 ModifierKeys.Shift |
                                 ModifierKeys.Windows;

        if ((modifiers & ~supported) != ModifierKeys.None)
        {
            throw new ArgumentException(
                "The hotkey contains an unsupported modifier.",
                nameof(modifiers));
        }

        return new PushToTalkHotkeyDefinition(key, modifiers);
    }

    public static bool TryParse(
        string? value,
        out PushToTalkHotkeyDefinition? definition)
    {
        definition = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        string[] parts = value.Split(
            '+',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        if (parts.Length == 0 ||
            !Enum.TryParse(parts[^1], ignoreCase: true, out Key key))
        {
            return false;
        }

        ModifierKeys modifiers = ModifierKeys.None;

        foreach (string part in parts[..^1])
        {
            modifiers |= part.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => ModifierKeys.Control,
                "ALT" => ModifierKeys.Alt,
                "SHIFT" => ModifierKeys.Shift,
                "WIN" or "WINDOWS" => ModifierKeys.Windows,
                _ => (ModifierKeys)(-1)
            };

            if ((int)modifiers < 0)
            {
                return false;
            }
        }

        try
        {
            definition = Create(key, modifiers);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public override string ToString()
    {
        List<string> parts = new();

        if (Modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(Key.ToString());
        return string.Join('+', parts);
    }

    public static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or
            Key.LWin or Key.RWin;
    }
}
