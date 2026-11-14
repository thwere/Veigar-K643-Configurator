using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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

    private readonly Dictionary<string, List<Border>> _keyGlows = new(StringComparer.Ordinal);

    public MainWindow()
    {
        InitializeComponent();

        FontFamily captionFont = GetCaptionFontFamily();
        MinimizeGlyph.FontFamily = captionFont;
        MaximizeGlyph.FontFamily = captionFont;
        CloseGlyph.FontFamily = captionFont;

        CreateKeyVisuals();

        SourceInitialized += (_, _) => DisableRoundedCorners();
        StateChanged += (_, _) => UpdateMaximizeGlyph();
        Deactivated += (_, _) => ClearPressedKeys();

        UpdateMaximizeGlyph();
    }

    private void CreateKeyVisuals()
    {
        Uri layoutUri = new("pack://application:,,,/assets/layout/veigar-k643-layout.json", UriKind.Absolute);
        var resource = Application.GetResourceStream(layoutUri);
        if (resource is null)
            throw new InvalidOperationException("Keyboard layout resource was not found.");

        using Stream stream = resource.Stream;
        KeyboardLayoutConfig? layout = JsonSerializer.Deserialize<KeyboardLayoutConfig>(
            stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (layout is null)
            throw new InvalidOperationException("Keyboard layout resource is empty or invalid.");

        foreach (KeyboardKeyConfig keyConfig in layout.Keys)
        foreach (KeyboardKeyArea area in keyConfig.Areas)
        {
            if (area.Width > 0 && area.Height > 0)
                AddKeyVisuals(keyConfig.Key, area.X, area.Y, area.Width, area.Height);
        }
    }

    private void AddKeyVisuals(string keyName, double left, double top, double width, double height)
    {
        Border glow = new()
        {
            Width = width,
            Height = height,
            Background = CreateRgbGlow(left + width / 2),
            CornerRadius = new CornerRadius(5),
            Opacity = 0,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(glow, left);
        Canvas.SetTop(glow, top);
        KeyGlowCanvas.Children.Add(glow);

        if (!_keyGlows.TryGetValue(keyName, out List<Border>? glows))
        {
            glows = new List<Border>();
            _keyGlows.Add(keyName, glows);
        }
        glows.Add(glow);

        // Expand symmetrically around the JSON rectangle's center.
        const double hitboxScale = 1.08;
        double hitboxWidth = width * hitboxScale;
        double hitboxHeight = height * hitboxScale;
        double hitboxLeft = left - (hitboxWidth - width) / 2;
        double hitboxTop = top - (hitboxHeight - height) / 2;

        Border hitbox = new()
        {
            Width = hitboxWidth,
            Height = hitboxHeight,
            Background = new SolidColorBrush(Color.FromArgb(18, 90, 155, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(48, 110, 175, 255)),
            BorderThickness = new Thickness(0.6),
            CornerRadius = new CornerRadius(4),
            Cursor = Cursors.Hand,
            Focusable = false
        };
        Canvas.SetLeft(hitbox, hitboxLeft);
        Canvas.SetTop(hitbox, hitboxTop);
        hitbox.MouseLeftButtonDown += (_, e) =>
        {
            SetKeyPressed(keyName, true);
            _ = Mouse.Capture(hitbox, CaptureMode.Element);
            e.Handled = true;
        };
        hitbox.MouseLeftButtonUp += (_, e) =>
        {
            SetKeyPressed(keyName, false);
            Mouse.Capture(null);
            e.Handled = true;
        };
        KeyHitboxCanvas.Children.Add(hitbox);
    }

    private void SetKeyPressed(string keyName, bool isPressed)
    {
        if (!_keyGlows.TryGetValue(keyName, out List<Border>? glows))
            return;

        foreach (Border glow in glows)
            glow.Opacity = isPressed ? 1 : 0;
    }

    private static Brush CreateRgbGlow(double centerX)
    {
        Color color = GetSpectrumColor(centerX / 599.0 * 300.0);
        RadialGradientBrush brush = new()
        {
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.5, 0.5),
            RadiusX = 0.78,
            RadiusY = 0.78
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(220, color.R, color.G, color.B), 0.0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(120, color.R, color.G, color.B), 0.42));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1.0));
        brush.Freeze();
        return brush;
    }

    private static Color GetSpectrumColor(double hue)
    {
        hue = Math.Max(0, Math.Min(300, hue));
        int sector = (int)(hue / 60);
        double fraction = hue / 60 - sector;
        double descending = 1 - fraction;
        return sector switch
        {
            0 => Color.FromRgb(255, (byte)(fraction * 255), 0),
            1 => Color.FromRgb((byte)(descending * 255), 255, 0),
            2 => Color.FromRgb(0, 255, (byte)(fraction * 255)),
            3 => Color.FromRgb(0, (byte)(descending * 255), 255),
            4 => Color.FromRgb((byte)(fraction * 255), 0, 255),
            _ => Color.FromRgb(255, 0, (byte)(descending * 255))
        };
    }

    private void ClearPressedKeys()
    {
        foreach (List<Border> glows in _keyGlows.Values)
        foreach (Border glow in glows)
            glow.Opacity = 0;

        Mouse.Capture(null);
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


internal sealed class KeyboardLayoutConfig
{
    public List<KeyboardKeyConfig> Keys { get; set; } = new();
}

internal sealed class KeyboardKeyConfig
{
    public string Key { get; set; } = string.Empty;
    public List<KeyboardKeyArea> Areas { get; set; } = new();
}

internal sealed class KeyboardKeyArea
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
