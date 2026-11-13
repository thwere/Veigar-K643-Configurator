using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace VeigarK643.Shell.Interop;

internal static class NativeWindowEffects
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmSystemBackdropType = 38;
    private const int DwmCornerPreferenceRound = 2;
    private const int DwmBackdropDesktopAcrylic = 3;

    private const int WindowCompositionAttributeAccentPolicy = 19;
    private const int AccentEnableAcrylicBlurBehind = 4;

    internal static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;

        if (handle == IntPtr.Zero)
        {
            return;
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            SetDwmAttribute(handle, DwmUseImmersiveDarkMode, 1);
            SetDwmAttribute(handle, DwmWindowCornerPreference, DwmCornerPreferenceRound);

            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
            {
                SetDwmAttribute(handle, DwmSystemBackdropType, DwmBackdropDesktopAcrylic);
            }
        }
        else if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            ApplyWindows10Acrylic(handle);
        }

        UpdateRoundedRegion(window);
    }

    internal static void UpdateRoundedRegion(Window window)
    {
        // Windows 11 provides native rounded corners through DWM.
        // On Windows 10, use a rounded native window region as a fallback.
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            var handle = new WindowInteropHelper(window).Handle;

            if (handle == IntPtr.Zero)
            {
                return;
            }

            if (window.WindowState == WindowState.Maximized)
            {
                SetWindowRgn(handle, IntPtr.Zero, redraw: true);
                return;
            }

            var dpi = VisualTreeHelper.GetDpi(window);
            var width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth * dpi.DpiScaleX));
            var height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight * dpi.DpiScaleY));
            var cornerDiameter = Math.Max(1, (int)Math.Ceiling(24 * dpi.DpiScaleX));

            var region = CreateRoundRectRgn(
                0,
                0,
                width + 1,
                height + 1,
                cornerDiameter,
                cornerDiameter);

            if (region != IntPtr.Zero && SetWindowRgn(handle, region, redraw: true) == 0)
            {
                DeleteObject(region);
            }
        }
    }

    private static void SetDwmAttribute(IntPtr handle, int attribute, int value)
    {
        _ = DwmSetWindowAttribute(
            handle,
            attribute,
            ref value,
            sizeof(int));
    }

    private static void ApplyWindows10Acrylic(IntPtr handle)
    {
        var policy = new AccentPolicy
        {
            AccentState = AccentEnableAcrylicBlurBehind,
            AccentFlags = 2,
            GradientColor = unchecked((int)0xCC20232D),
            AnimationId = 0
        };

        var size = Marshal.SizeOf<AccentPolicy>();
        var policyPointer = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.StructureToPtr(policy, policyPointer, fDeleteOld: false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttributeAccentPolicy,
                Data = policyPointer,
                SizeOfData = size
            };

            _ = SetWindowCompositionAttribute(handle, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(policyPointer);
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowCompositionAttribute(
        IntPtr hwnd,
        ref WindowCompositionAttributeData data);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateRoundRectRgn(
        int left,
        int top,
        int right,
        int bottom,
        int widthEllipse,
        int heightEllipse);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(
        IntPtr hwnd,
        IntPtr region,
        [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr objectHandle);

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        internal int AccentState;
        internal int AccentFlags;
        internal int GradientColor;
        internal int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        internal int Attribute;
        internal IntPtr Data;
        internal int SizeOfData;
    }
}
