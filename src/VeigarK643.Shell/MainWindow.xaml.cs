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
        // Coordinates are defined in the original 599x222 keyboard-artwork space.
        // The Viewbox scales the image and all overlays together without distortion.
        AddKeyHighlight(Key.Escape, 10, 8, 32, 34);

        AddKeyHighlight(Key.F1, 76, 8, 31, 34);
        AddKeyHighlight(Key.F2, 110, 8, 31, 34);
        AddKeyHighlight(Key.F3, 144, 8, 31, 34);
        AddKeyHighlight(Key.F4, 178, 8, 31, 34);
        AddKeyHighlight(Key.F5, 214, 8, 31, 34);
        AddKeyHighlight(Key.F6, 248, 8, 31, 34);
        AddKeyHighlight(Key.F7, 282, 8, 31, 34);
        AddKeyHighlight(Key.F8, 316, 8, 31, 34);
        AddKeyHighlight(Key.F9, 352, 8, 31, 34);
        AddKeyHighlight(Key.F10, 386, 8, 31, 34);
        AddKeyHighlight(Key.F11, 420, 8, 31, 34);
        AddKeyHighlight(Key.F12, 454, 8, 31, 34);
        AddKeyHighlight(Key.PrintScreen, 500, 8, 31, 34);
        AddKeyHighlight(Key.Scroll, 535, 8, 31, 34);
        AddKeyHighlight(Key.Pause, 569, 8, 20, 34);

        AddKeyHighlight(Key.OemTilde, 10, 47, 29, 30);
        AddKeyHighlight(Key.D1, 41, 47, 29, 30);
        AddKeyHighlight(Key.D2, 72, 47, 29, 30);
        AddKeyHighlight(Key.D3, 103, 47, 29, 30);
        AddKeyHighlight(Key.D4, 134, 47, 29, 30);
        AddKeyHighlight(Key.D5, 165, 47, 29, 30);
        AddKeyHighlight(Key.D6, 196, 47, 29, 30);
        AddKeyHighlight(Key.D7, 227, 47, 29, 30);
        AddKeyHighlight(Key.D8, 258, 47, 29, 30);
        AddKeyHighlight(Key.D9, 289, 47, 29, 30);
        AddKeyHighlight(Key.D0, 320, 47, 29, 30);
        AddKeyHighlight(Key.OemMinus, 351, 47, 29, 30);
        AddKeyHighlight(Key.OemPlus, 382, 47, 29, 30);
        AddKeyHighlight(Key.Back, 413, 47, 44, 30);

        AddKeyHighlight(Key.NumLock, 459, 47, 29, 30);
        AddKeyHighlight(Key.Divide, 490, 47, 29, 30);
        AddKeyHighlight(Key.Multiply, 521, 47, 29, 30);
        AddKeyHighlight(Key.Subtract, 552, 47, 29, 30);

        AddKeyHighlight(Key.Tab, 10, 81, 44, 30);
        AddKeyHighlight(Key.Q, 56, 81, 29, 30);
        AddKeyHighlight(Key.W, 87, 81, 29, 30);
        AddKeyHighlight(Key.E, 118, 81, 29, 30);
        AddKeyHighlight(Key.R, 149, 81, 29, 30);
        AddKeyHighlight(Key.T, 180, 81, 29, 30);
        AddKeyHighlight(Key.Y, 211, 81, 29, 30);
        AddKeyHighlight(Key.U, 242, 81, 29, 30);
        AddKeyHighlight(Key.I, 273, 81, 29, 30);
        AddKeyHighlight(Key.O, 304, 81, 29, 30);
        AddKeyHighlight(Key.P, 335, 81, 29, 30);
        AddKeyHighlight(Key.OemOpenBrackets, 366, 81, 29, 30);
        AddKeyHighlight(Key.OemCloseBrackets, 397, 81, 29, 30);
        AddKeyHighlight(Key.OemPipe, 428, 81, 29, 30);

        AddKeyHighlight(Key.NumPad7, 459, 81, 29, 30);
        AddKeyHighlight(Key.NumPad8, 490, 81, 29, 30);
        AddKeyHighlight(Key.NumPad9, 521, 81, 29, 30);
        AddKeyHighlight(Key.Add, 552, 81, 29, 64);

        AddKeyHighlight(Key.CapsLock, 10, 115, 54, 30);
        AddKeyHighlight(Key.A, 65, 115, 29, 30);
        AddKeyHighlight(Key.S, 96, 115, 29, 30);
        AddKeyHighlight(Key.D, 127, 115, 29, 30);
        AddKeyHighlight(Key.F, 158, 115, 29, 30);
        AddKeyHighlight(Key.G, 189, 115, 29, 30);
        AddKeyHighlight(Key.H, 220, 115, 29, 30);
        AddKeyHighlight(Key.J, 251, 115, 29, 30);
        AddKeyHighlight(Key.K, 282, 115, 29, 30);
        AddKeyHighlight(Key.L, 313, 115, 29, 30);
        AddKeyHighlight(Key.OemSemicolon, 344, 115, 29, 30);
        AddKeyHighlight(Key.OemQuotes, 375, 115, 29, 30);
        AddKeyHighlight(Key.Return, 406, 115, 51, 30);

        AddKeyHighlight(Key.NumPad4, 459, 115, 29, 30);
        AddKeyHighlight(Key.NumPad5, 490, 115, 29, 30);
        AddKeyHighlight(Key.NumPad6, 521, 115, 29, 30);

        AddKeyHighlight(Key.LeftShift, 10, 148, 69, 30);
        AddKeyHighlight(Key.Z, 81, 148, 29, 30);
        AddKeyHighlight(Key.X, 112, 148, 29, 30);
        AddKeyHighlight(Key.C, 143, 148, 29, 30);
        AddKeyHighlight(Key.V, 174, 148, 29, 30);
        AddKeyHighlight(Key.B, 205, 148, 29, 30);
        AddKeyHighlight(Key.N, 236, 148, 29, 30);
        AddKeyHighlight(Key.M, 267, 148, 29, 30);
        AddKeyHighlight(Key.OemComma, 298, 148, 29, 30);
        AddKeyHighlight(Key.OemPeriod, 329, 148, 29, 30);
        AddKeyHighlight(Key.OemQuestion, 360, 148, 29, 30);
        AddKeyHighlight(Key.RightShift, 391, 148, 66, 30);

        AddKeyHighlight(Key.NumPad1, 459, 148, 29, 30);
        AddKeyHighlight(Key.NumPad2, 490, 148, 29, 30);
        AddKeyHighlight(Key.NumPad3, 521, 148, 29, 30);
        AddKeyHighlight(Key.Enter, 552, 148, 29, 64);

        AddKeyHighlight(Key.LeftCtrl, 10, 182, 38, 30);
        AddKeyHighlight(Key.LWin, 50, 182, 38, 30);
        AddKeyHighlight(Key.LeftAlt, 90, 182, 38, 30);
        AddKeyHighlight(Key.Space, 130, 182, 190, 30);
        AddKeyHighlight(Key.RightAlt, 322, 182, 38, 30);
        AddKeyHighlight(Key.Apps, 362, 182, 38, 30);
        AddKeyHighlight(Key.RightCtrl, 402, 182, 55, 30);
        AddKeyHighlight(Key.NumPad0, 459, 182, 60, 30);
        AddKeyHighlight(Key.Decimal, 521, 182, 29, 30);
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
