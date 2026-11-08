using System;

namespace VeigarConfigurator.Windows.RawInput
{
    public sealed class RawKeyEventArgs : EventArgs
    {
        public RawKeyEventArgs(ushort virtualKey, ushort scanCode, bool isKeyDown, string devicePath)
        {
            VirtualKey = virtualKey;
            ScanCode = scanCode;
            IsKeyDown = isKeyDown;
            DevicePath = devicePath ?? string.Empty;
        }

        public ushort VirtualKey { get; private set; }
        public ushort ScanCode { get; private set; }
        public bool IsKeyDown { get; private set; }
        public string DevicePath { get; private set; }
    }
}
