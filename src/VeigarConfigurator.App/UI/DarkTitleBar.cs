using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace VeigarConfigurator.App.UI
{
    public static class DarkTitleBar
    {
        public static void Apply(Window window)
        {
            if (window == null) throw new ArgumentNullException("window");
            window.SourceInitialized += delegate
            {
                Set(window, true);
            };
        }

        public static void Set(Window window, bool enabled)
        {
            if (window == null) return;
            try
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                if (handle == IntPtr.Zero) return;
                int value = enabled ? 1 : 0;
                int result = DwmSetWindowAttribute(handle, 20, ref value, sizeof(int));
                if (result != 0)
                    DwmSetWindowAttribute(handle, 19, ref value, sizeof(int));
            }
            catch
            {
                // Native title-bar styling is optional and never blocks the app.
            }
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr window, int attribute, ref int value, int valueSize);
    }
}
