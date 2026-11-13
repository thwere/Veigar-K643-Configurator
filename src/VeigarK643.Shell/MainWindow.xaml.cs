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
        // One unit is 29 px; the 3 px inter-key gap makes each pitch 32 px.
        const double pitch = 32;
        const double keyUnit = 29;

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
                    FontSize = label.Length > 5 ? 8 : 10,
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

        // F row: Esc, three groups separated by 0.5u, Print Screen, and numpad-area controls.
        AddKey("Esc", 0, 0);
        string[] functionKeys =
        {
            "F1", "F2", "F3", "F4",
            "F5", "F6", "F7", "F8",
            "F9", "F10", "F11", "F12"
        };
        for (int i = 0; i < functionKeys.Length; i++)
        {
            double column = i < 4 ? 2 + i : i < 8 ? 6.5 + i - 4 : 11 + i - 8;
            AddKey(functionKeys[i], 0, column, accent: i >= 4 && i <= 7);
        }
        AddKey("PrtSc", 0, 16);
        AddKey("-", 0, 19);
        AddKey("+", 0, 20);

        // Row 1: 15u main block, followed by the three-key numpad top row.
        AddKey("`", 1, 0);
        string[] numberKeys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" };
        for (int i = 0; i < numberKeys.Length; i++)
            AddKey(numberKeys[i], 1, i + 1);
        AddKey("Backspace", 1, 13, 2);
        AddKey("NumLock", 1, 18);
        AddKey("/", 1, 19);
        AddKey("*", 1, 20);

        // Row 2.
        AddKey("Tab", 2, 0, 1.5);
        string[] qRow = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" };
        for (int i = 0; i < qRow.Length; i++)
            AddKey(qRow[i], 2, 1.5 + i);
        AddKey("[", 2, 11.5);
        AddKey("]", 2, 12.5);
        AddKey("\\", 2, 13.5, 1.5);
        AddKey("7", 2, 18);
        AddKey("8", 2, 19);
        AddKey("9", 2, 20);

        // Row 3. The numpad plus key spans rows 3 and 4.
        AddKey("CapsLock", 3, 0, 1.75);
        string[] homeRow = { "A", "S", "D", "F", "G", "H", "J", "K", "L" };
        for (int i = 0; i < homeRow.Length; i++)
            AddKey(homeRow[i], 3, 1.75 + i);
        AddKey(";", 3, 10.75);
        AddKey("'", 3, 11.75);
        AddKey("Enter", 3, 12.75, 2.25);
        AddKey("4", 3, 18);
        AddKey("5", 3, 19);
        AddKey("6", 3, 20);
        AddKey("+", 3, 21, heightRows: 2);

        // Row 4. Enter continues into row 5; Up is in the navigation column.
        AddKey("LShift", 4, 0, 2.25);
        string[] lowerRow = { "Z", "X", "C", "V", "B", "N", "M" };
        for (int i = 0; i < lowerRow.Length; i++)
            AddKey(lowerRow[i], 4, 2.25 + i);
        AddKey(",", 4, 9.25);
        AddKey(".", 4, 10.25);
        AddKey("/", 4, 11.25);
        AddKey("RShift", 4, 12.25, 2.75);
        AddKey("↑", 4, 16);
        AddKey("1", 4, 18);
        AddKey("2", 4, 19);
        AddKey("3", 4, 20);

        // Row 5: modifiers, three-key arrow cluster, and the numpad bottom row.
        AddKey("LCtrl", 5, 0, 1.25);
        AddKey("Win", 5, 1.25);
        AddKey("LAlt", 5, 2.25);
        AddKey("Space", 5, 3.25, 6.25);
        AddKey("RAlt", 5, 9.5);
        AddKey("Fn", 5, 10.5);
        AddKey("RCtrl", 5, 11.5);
        AddKey("←", 5, 15);
        AddKey("↓", 5, 16);
        AddKey("→", 5, 17);
        AddKey("0", 5, 18, 2);
        AddKey(".", 5, 20);
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
