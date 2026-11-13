using System;
using System.IO;
using System.Windows;

namespace VeigarK643.Shell;

internal static class WindowStateStore
{
    private static readonly string StateFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "VeigarK643Configurator",
        "window-state.txt");

    internal static bool ShouldStartMaximized()
    {
        try
        {
            return File.Exists(StateFilePath)
                && string.Equals(
                    File.ReadAllText(StateFilePath).Trim(),
                    "Maximized",
                    StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static void Save(WindowState state)
    {
        try
        {
            var directory = Path.GetDirectoryName(StateFilePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                StateFilePath,
                state == WindowState.Maximized ? "Maximized" : "Normal");
        }
        catch (IOException)
        {
            // Window-state persistence is best-effort only.
        }
        catch (UnauthorizedAccessException)
        {
            // Window-state persistence is best-effort only.
        }
    }
}
