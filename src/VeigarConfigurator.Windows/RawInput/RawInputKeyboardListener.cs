using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace VeigarConfigurator.Windows.RawInput
{
    /// <summary>
    /// Receives transient key events from the tested K643 USB device only.
    /// It never records typed text or suppresses the normal keyboard input path.
    /// </summary>
    public sealed class RawInputKeyboardListener : IDisposable
    {
        private const int WmInput = 0x00FF;
        private const uint RidInput = 0x10000003;
        private const uint RidiDeviceName = 0x20000007;
        private const uint RimTypeKeyboard = 1;
        private const ushort RiKeyBreak = 0x0001;
        private const ushort RiKeyE0 = 0x0002;
        private const uint RidevRemove = 0x00000001;
        private const uint RidevInputSink = 0x00000100;
        private const ushort HidUsagePageGeneric = 0x01;
        private const ushort HidUsageKeyboard = 0x06;

        private HwndSource _source;
        private IntPtr _windowHandle;
        private bool _registered;
        private bool _disposed;

        public event EventHandler<RawKeyEventArgs> KeyReceived;

        public bool Register(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                throw new ArgumentException("A valid window handle is required.", "windowHandle");
            }

            if (_disposed)
            {
                throw new ObjectDisposedException("RawInputKeyboardListener");
            }

            _windowHandle = windowHandle;
            _source = HwndSource.FromHwnd(_windowHandle);
            if (_source == null)
            {
                return false;
            }

            _source.AddHook(WindowProc);

            var device = new RawInputDevice
            {
                UsagePage = HidUsagePageGeneric,
                Usage = HidUsageKeyboard,
                Flags = RidevInputSink,
                Target = _windowHandle
            };

            _registered = RegisterRawInputDevices(
                ref device,
                1,
                (uint)Marshal.SizeOf(typeof(RawInputDevice)));

            return _registered;
        }

        private IntPtr WindowProc(
            IntPtr hwnd,
            int message,
            IntPtr wParam,
            IntPtr lParam,
            ref bool handled)
        {
            if (message == WmInput)
            {
                ProcessRawInput(lParam);
            }

            // Never mark WM_INPUT handled: the OS still delivers the original key.
            return IntPtr.Zero;
        }

        private void ProcessRawInput(IntPtr rawInputHandle)
        {
            uint size = 0;
            uint headerSize = (uint)Marshal.SizeOf(typeof(RawInputHeader));
            uint result = GetRawInputData(rawInputHandle, RidInput, IntPtr.Zero, ref size, headerSize);
            if (result == UInt32.MaxValue || size < headerSize)
            {
                return;
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                result = GetRawInputData(rawInputHandle, RidInput, buffer, ref size, headerSize);
                if (result == UInt32.MaxValue)
                {
                    return;
                }

                var header = (RawInputHeader)Marshal.PtrToStructure(
                    buffer,
                    typeof(RawInputHeader));

                if (header.Type != RimTypeKeyboard)
                {
                    return;
                }

                string devicePath = GetDevicePath(header.Device);
                if (!IsVeigarK643(devicePath))
                {
                    return;
                }

                IntPtr keyboardPointer = IntPtr.Add(buffer, (int)headerSize);
                var keyboard = (RawKeyboard)Marshal.PtrToStructure(
                    keyboardPointer,
                    typeof(RawKeyboard));

                bool isKeyDown = (keyboard.Flags & RiKeyBreak) == 0;
                bool isExtended = (keyboard.Flags & RiKeyE0) != 0;
                EventHandler<RawKeyEventArgs> handler = KeyReceived;
                if (handler != null)
                {
                    handler(this, new RawKeyEventArgs(
                        keyboard.VirtualKey,
                        keyboard.MakeCode,
                        isKeyDown,
                        devicePath,
                        isExtended));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static bool IsVeigarK643(string devicePath)
        {
            return !string.IsNullOrEmpty(devicePath) &&
                   devicePath.IndexOf("VID_258A", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   devicePath.IndexOf("PID_0049", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetDevicePath(IntPtr deviceHandle)
        {
            uint characterCount = 0;
            GetRawInputDeviceInfo(deviceHandle, RidiDeviceName, IntPtr.Zero, ref characterCount);
            if (characterCount == 0 || characterCount > 32768)
            {
                return string.Empty;
            }

            IntPtr nameBuffer = Marshal.AllocHGlobal((int)(characterCount * 2));
            try
            {
                uint size = characterCount;
                uint result = GetRawInputDeviceInfo(
                    deviceHandle,
                    RidiDeviceName,
                    nameBuffer,
                    ref size);

                if (result == UInt32.MaxValue)
                {
                    return string.Empty;
                }

                return Marshal.PtrToStringUni(nameBuffer) ?? string.Empty;
            }
            finally
            {
                Marshal.FreeHGlobal(nameBuffer);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_source != null)
            {
                _source.RemoveHook(WindowProc);
                _source = null;
            }

            if (_registered)
            {
                var device = new RawInputDevice
                {
                    UsagePage = HidUsagePageGeneric,
                    Usage = HidUsageKeyboard,
                    Flags = RidevRemove,
                    Target = IntPtr.Zero
                };

                RegisterRawInputDevices(
                    ref device,
                    1,
                    (uint)Marshal.SizeOf(typeof(RawInputDevice)));
                _registered = false;
            }

            _windowHandle = IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputDevice
        {
            public ushort UsagePage;
            public ushort Usage;
            public uint Flags;
            public IntPtr Target;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputHeader
        {
            public uint Type;
            public uint Size;
            public IntPtr Device;
            public IntPtr WParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RawKeyboard
        {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VirtualKey;
            public uint Message;
            public uint ExtraInformation;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(
            ref RawInputDevice rawInputDevices,
            uint numberOfDevices,
            uint size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(
            IntPtr rawInput,
            uint command,
            IntPtr data,
            ref uint size,
            uint headerSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceInfo(
            IntPtr device,
            uint command,
            IntPtr data,
            ref uint size);
    }
}
