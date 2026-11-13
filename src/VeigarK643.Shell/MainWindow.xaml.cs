using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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
