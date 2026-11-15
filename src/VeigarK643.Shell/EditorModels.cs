using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace VeigarK643.Shell;

internal sealed class KeyboardLayoutConfig { public List<KeyboardKeyConfig> Keys { get; set; } = new(); }
internal sealed class KeyboardKeyConfig { public string Key { get; set; } = string.Empty; public List<KeyboardKeyArea> Areas { get; set; } = new(); }
internal sealed class KeyboardKeyArea { public double X { get; set; } public double Y { get; set; } public double Width { get; set; } public double Height { get; set; } }

internal sealed class EditorSnapshot
{
    public Dictionary<string, Color> KeyColors { get; set; } = new(StringComparer.Ordinal);
    public List<EditorGroupData> Groups { get; set; } = new();
}

internal sealed class EditorGroup
{
    public string Name { get; set; } = "Группа";
    public string ColorHex { get; set; } = "#FF6D8CFF";
    public string State { get; set; } = "Статичный";
    public HashSet<string> Keys { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class EditorGroupData
{
    public string Name { get; set; } = "Группа";
    public string ColorHex { get; set; } = "#FF6D8CFF";
    public string State { get; set; } = "Статичный";
    public List<string> Keys { get; set; } = new();
}

internal sealed class EditorProfileData
{
    public string Name { get; set; } = "Основной";
    public string ColorHex { get; set; } = "#FF6D8CFF";
    public Dictionary<string, string> KeyColors { get; set; } = new(StringComparer.Ordinal);
    public List<EditorGroupData> Groups { get; set; } = new();
}
