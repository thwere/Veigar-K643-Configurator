using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace VeigarK643.Shell;

public partial class MainWindow : Window
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpDoNotRound = 1;

    private readonly Dictionary<Key, List<Border>> _keyHighlights = new();

    public MainWindow()
    {
        InitializeComponent();

        FontFamily captionFont = GetCaptionFontFamily();
        MinimizeGlyph.FontFamily = captionFont;
        MaximizeGlyph.FontFamily = captionFont;
        CloseGlyph.FontFamily = captionFont;

        CreateKeyHighlights();

        SourceInitialized += (_, _) => DisableRoundedCorners();
        StateChanged += (_, _) => UpdateMaximizeGlyph();
        Loaded += (_, _) => Keyboard.Focus(this);
        Deactivated += (_, _) => ClearPressedKeys();
        PreviewMouseDown += (_, _) => Keyboard.Focus(this);
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewKeyUp += MainWindow_PreviewKeyUp;

        UpdateMaximizeGlyph();
    }

    private void CreateKeyHighlights()
    {
        Uri layoutUri = new("pack://application:,,,/assets/layout/veigar-k643-layout.json", UriKind.Absolute);
        var resource = Application.GetResourceStream(layoutUri);
        if (resource is null)
        {
            throw new InvalidOperationException("Keyboard layout resource was not found.");
        }

        using Stream stream = resource.Stream;
        KeyboardLayoutConfig? layout = JsonSerializer.Deserialize<KeyboardLayoutConfig>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (layout is null)
        {
            throw new InvalidOperationException("Keyboard layout resource is empty or invalid.");
        }

        foreach (KeyboardKeyConfig keyConfig in layout.Keys)
        {
            if (!TryMapKey(keyConfig.Key, out Key key))
            {
                continue;
            }

            foreach (KeyboardKeyArea area in keyConfig.Areas)
            {
                if (area.Width <= 0 || area.Height <= 0)
                {
                    continue;
                }

                AddKeyHighlight(key, area.X, area.Y, area.Width, area.Height);
            }
        }
    }

    private static bool TryMapKey(string keyName, out Key key)
    {
        switch (keyName)
        {
            case "PrtSc":
                key = Key.PrintScreen;
                return true;
            case "Capital":
                key = Key.CapsLock;
                return true;
            case "Oem3":
                key = Key.OemTilde;
                return true;
            case "Oem5":
                key = Key.OemPipe;
                return true;
            case "Oem6":
                key = Key.OemCloseBrackets;
                return true;
            case "Fn":
                // The region is retained in the layout, but Windows/WPF does not
                // normally expose the keyboard's firmware-level Fn key as an event.
                key = Key.None;
                return true;
            default:
                return Enum.TryParse(keyName, true, out key);
        }
    }

    private void AddKeyHighlight(Key key, double left, double top, double width, double height)
    {
        Border highlight = new()
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(Color.FromArgb(125, 73, 133, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(235, 145, 190, 255)),
            BorderThickness = new Thickness(1.2),
            CornerRadius = new CornerRadius(4),
            Opacity = 0,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(highlight, left);
        Canvas.SetTop(highlight, top);
        KeyOverlayCanvas.Children.Add(highlight);

        if (!_keyHighlights.TryGetValue(key, out List<Border>? highlights))
        {
            highlights = new List<Border>();
            _keyHighlights.Add(key, highlights);
        }

        highlights.Add(highlight);
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        Key key = GetKeyboardKey(e);
        if (_keyHighlights.TryGetValue(key, out List<Border>? highlights))
        {
            foreach (Border highlight in highlights)
            {
                highlight.Opacity = 1;
            }
        }
    }

    private void MainWindow_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        Key key = GetKeyboardKey(e);
        if (_keyHighlights.TryGetValue(key, out List<Border>? highlights))
        {
            foreach (Border highlight in highlights)
            {
                highlight.Opacity = 0;
            }
        }
    }

    private static Key GetKeyboardKey(KeyEventArgs e)
    {
        return e.Key == Key.System ? e.SystemKey : e.Key;
    }

    private void ClearPressedKeys()
    {
        foreach (List<Border> highlights in _keyHighlights.Values)
        {
            foreach (Border highlight in highlights)
            {
                highlight.Opacity = 0;
            }
        }
    }

    private static FontFamily GetCaptionFontFamily()
    {
        foreach (FontFamily font in Fonts.SystemFontFamilies)
        {
            if (string.Equals(
                font.Source,
                "Segoe Fluent Icons",
                StringComparison.OrdinalIgnoreCase))
            {
                return font;
            }
        }

        return new FontFamily("Segoe MDL2 Assets");
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdateMaximizeGlyph()
    {
        MaximizeGlyph.Text = WindowState == WindowState.Maximized
            ? "\uE923"
            : "\uE922";
    }

    private void DisableRoundedCorners()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        int preference = DwmwcpDoNotRound;
        _ = DwmSetWindowAttribute(
            handle,
            DwmwaWindowCornerPreference,
            ref preference,
            sizeof(int));
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
