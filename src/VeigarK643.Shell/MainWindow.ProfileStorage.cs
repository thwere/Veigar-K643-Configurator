using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using IOPath = System.IO.Path;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VeigarK643.Shell;

public partial class MainWindow : Window
{
    private void ScheduleProfileSave()
    {
        _profileSaveTimer.Stop();
        _profileSaveTimer.Start();
    }

    private void InitializeProfiles()
    {
        Directory.CreateDirectory(_profilesDirectory);
        LoadProfileOrder();
        string[] files = GetOrderedProfileFiles();
        if (files.Length == 0)
        {
            if (!_profileOrder.Contains(SafeFileName(_activeProfileName), StringComparer.OrdinalIgnoreCase))
                _profileOrder.Insert(0, SafeFileName(_activeProfileName));
            SaveProfileOrder();
            SaveCurrentProfile();
            return;
        }

        // The first profile in the saved order is the profile loaded when the application starts.
        LoadProfile(files[0]);
    }

    private void LoadProfileOrder()
    {
        try
        {
            if (File.Exists(_profileOrderPath))
            {
                if (new FileInfo(_profileOrderPath).Length > MaxProfileFileBytes)
                    throw new InvalidDataException("Profile order file is too large.");
                List<string>? loadedOrder = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_profileOrderPath), ProfileJsonOptions);
                if (loadedOrder is not null)
                    _profileOrder.AddRange(loadedOrder.Where(name => !string.IsNullOrWhiteSpace(name)));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Profile order load failed: " + ex.Message);
            _profileOrder.Clear();
        }

        string[] existing = Directory.GetFiles(_profilesDirectory, "*.json")
            .Select(path => IOPath.GetFileNameWithoutExtension(path))
            .ToArray();

        if (_profileOrder.Count == 0 && existing.Contains(SafeFileName(_activeProfileName), StringComparer.OrdinalIgnoreCase))
            _profileOrder.Add(SafeFileName(_activeProfileName));

        List<string> normalized = _profileOrder
            .Where(name => !string.IsNullOrWhiteSpace(name) && existing.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string name in existing.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            if (!normalized.Contains(name, StringComparer.OrdinalIgnoreCase))
                normalized.Add(name);

        _profileOrder.Clear();
        _profileOrder.AddRange(normalized);
        SaveProfileOrder();
    }

    private string[] GetOrderedProfileFiles()
    {
        if (!Directory.Exists(_profilesDirectory)) return Array.Empty<string>();
        Dictionary<string, int> orderByName = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _profileOrder.Count; i++)
            orderByName.TryAdd(_profileOrder[i], i);

        return Directory.GetFiles(_profilesDirectory, "*.json")
            .OrderBy(path => orderByName.TryGetValue(IOPath.GetFileNameWithoutExtension(path), out int index) ? index : int.MaxValue)
            .ThenBy(path => IOPath.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void SaveProfileOrder()
    {
        try
        {
            Directory.CreateDirectory(_profilesDirectory);
            WriteAllTextAtomically(_profileOrderPath, JsonSerializer.Serialize(_profileOrder, ProfileJsonOptions));
        }
        catch (Exception ex) { Debug.WriteLine("Profile order save failed: " + ex.Message); }
    }

    private void RenameProfileOrderEntry(string? oldPath, string newPath)
    {
        string newName = IOPath.GetFileNameWithoutExtension(newPath);
        if (oldPath is not null)
        {
            string oldName = IOPath.GetFileNameWithoutExtension(oldPath);
            int index = _profileOrder.FindIndex(name => string.Equals(name, oldName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) _profileOrder[index] = newName;
        }
        if (!_profileOrder.Contains(newName, StringComparer.OrdinalIgnoreCase))
            _profileOrder.Add(newName);
        SaveProfileOrder();
    }

    private void CommitProfileOrder(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex >= _profileOrder.Count || targetIndex >= _profileOrder.Count) return;
        string moved = _profileOrder[sourceIndex];
        _profileOrder.RemoveAt(sourceIndex);
        _profileOrder.Insert(targetIndex, moved);
        SaveProfileOrder();
        ToolStatusText.Text = $"Приоритет профилей обновлён. «{moved}» — позиция {targetIndex + 1}.";
    }

    private void AttachProfileDragDrop(StackPanel body, Grid row, TextBlock handle)
    {
        AttachAnimatedLocalReorder(
            body,
            row,
            handle,
            () => body.Children.OfType<Grid>().Where(item => item.Tag is string).ToList(),
            CommitProfileOrder);
    }

    private static string SafeFileName(string name)
    {
        foreach (char c in IOPath.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim().TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(name)) return "Profile";

        string deviceName = name.Split('.')[0];
        if (deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            (deviceName.Length == 4 &&
             (deviceName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || deviceName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
             deviceName[3] is >= '1' and <= '9'))
            name = "_" + name;

        return name;
    }

    private string ProfilePath(string name) => IOPath.Combine(_profilesDirectory, SafeFileName(name) + ".json");

    private static void WriteAllTextAtomically(string path, string content)
    {
        string directory = IOPath.GetDirectoryName(path) ?? throw new InvalidOperationException("A target directory is required.");
        Directory.CreateDirectory(directory);
        string temporaryPath = IOPath.Combine(directory, $".{IOPath.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch (Exception cleanupError)
            {
                Debug.WriteLine("Temporary profile cleanup failed: " + cleanupError.Message);
            }
        }
    }

    private static bool TryParseProfileColor(string? value, out Color color)
    {
        try
        {
            if (ColorConverter.ConvertFromString(value) is Color parsed)
            {
                color = parsed;
                return true;
            }
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or NotSupportedException)
        {
            // Invalid profile color values are rejected during profile normalization.
        }

        color = Colors.Transparent;
        return false;
    }

    private static EditorProfileData ReadProfileFile(string path, bool requireName = false)
    {
        FileInfo file = new(path);
        if (file.Length > MaxProfileFileBytes)
            throw new InvalidDataException("Profile file exceeds the 1 MiB safety limit.");

        EditorProfileData? data = JsonSerializer.Deserialize<EditorProfileData>(File.ReadAllText(path), ProfileJsonOptions);
        if (data is null) throw new InvalidDataException("Profile file is empty or invalid.");
        if (requireName && string.IsNullOrWhiteSpace(data.Name))
            throw new InvalidDataException("Profile name is required.");

        data.Name = string.IsNullOrWhiteSpace(data.Name) ? IOPath.GetFileNameWithoutExtension(path) : data.Name.Trim();
        if (!TryParseProfileColor(data.ColorHex, out _)) data.ColorHex = "#FF6D8CFF";

        Dictionary<string, string> keyColors = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in data.KeyColors ?? new Dictionary<string, string>())
            if (!string.IsNullOrWhiteSpace(entry.Key) && TryParseProfileColor(entry.Value, out _))
                keyColors[entry.Key] = entry.Value;
        if (keyColors.Count > 512) throw new InvalidDataException("Profile contains too many key color entries.");
        data.KeyColors = keyColors;

        List<EditorGroupData> groups = new();
        foreach (EditorGroupData? group in data.Groups ?? new List<EditorGroupData>())
        {
            if (group is null) continue;
            group.Name = string.IsNullOrWhiteSpace(group.Name) ? "Группа" : group.Name.Trim();
            if (!TryParseProfileColor(group.ColorHex, out _)) group.ColorHex = "#FF6D8CFF";
            group.State = string.IsNullOrWhiteSpace(group.State) ? "Статичный" : group.State;
            group.Keys = (group.Keys ?? new List<string>())
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (group.Keys.Count > 512) throw new InvalidDataException("A group contains too many keys.");
            groups.Add(group);
        }
        if (groups.Count > 128) throw new InvalidDataException("Profile contains too many groups.");
        data.Groups = groups;
        return data;
    }

    private bool SaveCurrentProfile()
    {
        try
        {
            Directory.CreateDirectory(_profilesDirectory);
            EditorProfileData data = new()
            {
                Name = _activeProfileName,
                ColorHex = _activeProfileColor,
                KeyColors = _keyColors.ToDictionary(pair => pair.Key, pair => $"#{pair.Value.R:X2}{pair.Value.G:X2}{pair.Value.B:X2}", StringComparer.Ordinal),
                Groups = _groups.Select(group => new EditorGroupData { Name = group.Name, ColorHex = group.ColorHex, State = group.State, Keys = group.Keys.ToList() }).ToList()
            };
            WriteAllTextAtomically(ProfilePath(_activeProfileName), JsonSerializer.Serialize(data, ProfileJsonOptions));
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Profile save failed: " + ex.Message);
            ToolStatusText.Text = "Не удалось сохранить профиль. Переключение профиля отменено.";
            return false;
        }
    }

    private void LoadProfile(string path)
    {
        try
        {
            EditorProfileData data = ReadProfileFile(path);
            Dictionary<string, Color> loadedColors = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in data.KeyColors)
                if (TryParseProfileColor(pair.Value, out Color color))
                    loadedColors[pair.Key] = color;

            List<EditorGroup> loadedGroups = data.Groups
                .Select(group => new EditorGroup
                {
                    Name = group.Name,
                    ColorHex = group.ColorHex,
                    State = group.State,
                    Keys = new HashSet<string>(group.Keys, StringComparer.Ordinal)
                })
                .ToList();

            // Parse and validate the entire profile before changing the active editor state.
            _profileSaveTimer.Stop();
            _keyColors.Clear();
            foreach (KeyValuePair<string, Color> pair in loadedColors) _keyColors[pair.Key] = pair.Value;
            foreach (string key in _keyGlows.Keys) UpdateKeyVisual(key);
            _groups.Clear();
            _groups.AddRange(loadedGroups);
            _selectedKeys.Clear();
            _activeProfileName = data.Name;
            _activeProfileColor = data.ColorHex;
            RenderSelection();
            ScheduleHardwareFrame();
            UpdateProfileButton();
            if (_groupManagerContent is not null) ShowGroupsManager();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Profile load failed: " + ex.Message);
            ToolStatusText.Text = "Не удалось загрузить профиль. Проверьте файл профиля.";
        }
    }
}
