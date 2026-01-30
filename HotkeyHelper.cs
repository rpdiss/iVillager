using System.Windows.Input;

namespace iVillager;

public static class HotkeyHelper
{
    /// <summary>
    /// Parsuje string np. "Ctrl+Shift+F1" na Key i ModifierKeys.
    /// </summary>
    public static bool TryParse(string? shortcut, out Key key, out ModifierKeys modifiers)
    {
        key = Key.None;
        modifiers = ModifierKeys.None;
        if (string.IsNullOrWhiteSpace(shortcut))
            return false;

        var parts = shortcut.Trim().Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        foreach (var part in parts.Take(parts.Length - 1))
        {
            var mod = part.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => ModifierKeys.Control,
                "ALT" => ModifierKeys.Alt,
                "SHIFT" => ModifierKeys.Shift,
                "WIN" or "WINDOWS" => ModifierKeys.Windows,
                _ => (ModifierKeys?)null
            };
            if (mod == null)
                return false;
            modifiers |= mod.Value;
        }

        var keyPart = parts[^1];
        if (!Enum.TryParse<Key>(keyPart, true, out key) || key == Key.None)
            return false;

        return true;
    }

    public static string ToString(Key key, ModifierKeys modifiers)
    {
        var parts = new List<string>();
        if ((modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
        if ((modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");
        if ((modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
        if ((modifiers & ModifierKeys.Windows) != 0) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
