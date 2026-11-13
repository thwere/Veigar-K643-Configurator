using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Controls;
using System.Windows.Media;

namespace VeigarK643.Shell;

public partial class MainWindow : Window
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpDoNotRound = 1;

    public MainWindow()
    {
        InitializeComponent();

        FontFamily captionFont = GetCaptionFontFamily();
        MinimizeGlyph.FontFamily = captionFont;
        MaximizeGlyph.FontFamily = captionFont;
        CloseGlyph.FontFamily = captionFont;

        SourceInitialized += (_, _) => DisableRoundedCorners();
        StateChanged += (_, _) => UpdateMaximizeGlyph();

        UpdateMaximizeGlyph();
        BuildKeyboardLayout();
    }

    private void BuildKeyboardLayout()
    {
        const double pitch = 37;
        const double keyUnit = 34;

        void AddKey(string label, int row, double column, double width = 1,
            bool accent = false, int heightRows = 1)
        {
            double keyWidth = width * keyUnit + (width - 1) * 3;
            double keyHeight = heightRows * keyUnit + (heightRows - 1) * 3;

            var key = new Border
            {
                Width = keyWidth,
                Height = keyHeight,
                CornerRadius = new CornerRadius(3),
                Background = accent
                    ? new SolidColorBrush(Color.FromRgb(75, 157, 153))
                    : new SolidColorBrush(Color.FromRgb(246, 247, 244)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(190, 197, 198)),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = label,
                    Foreground = accent
                        ? new SolidColorBrush(Color.FromRgb(247, 250, 248))
                        : new SolidColorBrush(Color.FromRgb(48, 57, 62)),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = label.Length > 4 ? 8 : 10,
                    FontWeight = FontWeights.Medium,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            Canvas.SetLeft(key, column * pitch);
            Canvas.SetTop(key, row * pitch);
            KeyboardCanvas.Children.Add(key);
        }

        // Function row
        AddKey("Esc", 0, 0);
        string[] functionKeys = { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12" };
        for (int i = 0; i < functionKeys.Length; i++)
        {
            double column = i < 4 ? i + 2 : i < 8 ? i + 3 : i + 4;
            AddKey(functionKeys[i], 0, column, accent: i >= 4 && i <= 7);
        }
        AddKey("PrtSc", 0, 17);
        AddKey("−", 0, 18);
        AddKey("+", 0, 19);

        // Number row and integrated numeric keypad
        string[] numberRow = { "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "Backspace" };
        double numberColumn = 0;
        for (int i = 0; i < numberRow.Length; i++)
        {
            double width = i == 13 ? 2 : 1;
            AddKey(numberRow[i], 1, numberColumn, width);
            numberColumn += width;
        }
        AddKey("Num", 1, 15);
        AddKey("/", 1, 16);
        AddKey("*", 1, 17);
        AddKey("−", 1, 18);

        // Q row
        AddKey("Tab", 2, 0, 1.5);
        string[] qRow = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" };
        for (int i = 0; i < qRow.Length; i++) AddKey(qRow[i], 2, 1.5 + i);
        AddKey("[", 2, 11.5);
        AddKey("]", 2, 12.5);
        AddKey("\\", 2, 13.5, 1.5);
        AddKey("7", 2, 15);
        AddKey("8", 2, 16);
        AddKey("9", 2, 17);
        AddKey("+", 2, 18, heightRows: 2);

        // Home row
        AddKey("Caps", 3, 0, 1.75);
        string[] homeRow = { "A", "S", "D", "F", "G", "H", "J", "K", "L" };
        for (int i = 0; i < homeRow.Length; i++) AddKey(homeRow[i], 3, 1.75 + i);
        AddKey(";", 3, 10.75);
        AddKey("'", 3, 11.75);
        AddKey("Enter", 3, 12.75, 2.25);
        AddKey("4", 3, 15);
        AddKey("5", 3, 16);
        AddKey("6", 3, 17);

        // Lower letter row, arrows and numeric keypad
        AddKey("Shift", 4, 0, 2.25);
        string[] lowerRow = { "Z", "X", "C", "V", "B", "N", "M" };
        for (int i = 0; i < lowerRow.Length; i++) AddKey(lowerRow[i], 4, 2.25 + i);
        AddKey(",", 4, 9.25);
        AddKey(".", 4, 10.25);
        AddKey("/", 4, 11.25);
        AddKey("Shift", 4, 12.25, 1.75);
        AddKey("↑", 4, 14);
        AddKey("1", 4, 15);
        AddKey("2", 4, 16);
        AddKey("3", 4, 17);
        AddKey("Enter", 4, 18, heightRows: 2);

        // Bottom row
        AddKey("Ctrl", 5, 0, 1.25);
        AddKey("Win", 5, 1.25, 1.25);
        AddKey("Alt", 5, 2.5, 1.25);
        AddKey("", 5, 3.75, 6.25);
        AddKey("Alt", 5, 10, 1.25);
        AddKey("Fn", 5, 11.25, 1.25);
        AddKey("Ctrl", 5, 12.5, 1.25);
        AddKey("←", 5, 14);
        AddKey("↓", 5, 15);
        AddKey("→", 5, 16);
        AddKey("0", 5, 17, 2);
        AddKey(".", 5, 19);
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
