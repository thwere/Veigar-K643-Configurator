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
            if (window == null)
            {
                throw new ArgumentNullException("window");
            }

            window.SourceInitialized += delegate
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                int enabled = 1;

                // DWMWA_USE_IMMERSIVE_DARK_MODE is 20 on current Windows 10/11.
                int result = DwmSetWindowAttribute(
                    handle,
                    20,
                    ref enabled,
                    sizeof(int));

                // Older supported Windows 10 builds used attribute 19.
                if (result != 0)
                {
                    DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
                }
            };
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr window,
            int attribute,
            ref int value,
            int valueSize);
    }
}
