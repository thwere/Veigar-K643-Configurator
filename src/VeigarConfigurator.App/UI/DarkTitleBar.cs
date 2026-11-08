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
                try
                {
                    IntPtr handle = new WindowInteropHelper(window).Handle;
                    int enabled = 1;
                    int result = DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
                    if (result != 0)
                        DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
                }
                catch
                {
                    // Dark title-bar styling is optional and must never block startup.
                }
            };
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);
    }
}
