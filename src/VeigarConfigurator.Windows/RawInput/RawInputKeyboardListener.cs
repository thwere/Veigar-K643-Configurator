using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using VeigarConfigurator.Windows.Diagnostics;

namespace VeigarConfigurator.Windows.RawInput
{
    /// <summary>
    /// Receives transient key events from the tested K643 USB device only.
    /// It never records typed text or suppresses the normal keyboard input path.
    /// </summary>
    public sealed class RawInputKeyboardListener : IDisposable
    {
        private const int WmInput = 0x00FF;
        private const int WmInputDeviceChange = 0x00FE;
        private const uint RidInput = 0x10000003;
        private const uint RidiDeviceName = 0x20000007;
        private const uint RimTypeKeyboard = 1;
        private const ushort RiKeyBreak = 0x0001;
        private const ushort RiKeyE0 = 0x0002;
        private const uint RidevRemove = 0x00000001;
        private const uint RidevInputSink = 0x00000100;
        private const uint RidevDevNotify = 0x00002000;
        private const ushort HidUsagePageGeneric = 0x01;
        private const ushort HidUsageKeyboard = 0x06;
        private const uint GidcArrival = 1;
        private const uint GidcRemoval = 2;

        private readonly HashSet<string> _pressedKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _reportedOtherDevices =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private HwndSource _source;
        private IntPtr _windowHandle;
        private bool _registered;
        private bool _disposed;
        private int _readFailureCount;

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
                DiagnosticLog.Warning("RAWINPUT", "Could not obtain an HwndSource for the main window.");
                return false;
            }

            _source.AddHook(WindowProc);

            var device = new RawInputDevice
            {
                UsagePage = HidUsagePageGeneric,
                Usage = HidUsageKeyboard,
                Flags = RidevInputSink | RidevDevNotify,
                Target = _windowHandle
            };

            _registered = RegisterRawInputDevices(
                ref device,
                1,
                (uint)Marshal.SizeOf(typeof(RawInputDevice)));

            if (!_registered)
            {
                int errorCode = Marshal.GetLastWin32Error();
                DiagnosticLog.Error(
                    "RAWINPUT",
                    "RegisterRawInputDevices failed. Win32 error " + errorCode + ": "
                        + new Win32Exception(errorCode).Message,
                    null);
                _source.RemoveHook(WindowProc);
                _source = null;
                return false;
            }

            DiagnosticLog.Info(
                "RAWINPUT",
                "Keyboard Raw Input registered. HWND=0x" + _windowHandle.ToInt64().ToString("X")
                    + ", flags=INPUTSINK|DEVNOTIFY, filter=VID_258A PID_0049.");

            LogKeyboardDevices();
            return true;
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
                try
                {
                    ProcessRawInput(lParam);
                }
                catch (Exception exception)
                {
                    DiagnosticLog.Error("RAWINPUT", "Unhandled error while processing WM_INPUT.", exception);
                }
            }
            else if (message == WmInputDeviceChange)
            {
                uint change = unchecked((uint)wParam.ToInt64());
                if (change == GidcArrival)
                {
                    DiagnosticLog.Info("RAWINPUT", "A raw-input device was connected.");
                    LogKeyboardDevices();
                }
                else if (change == GidcRemoval)
                {
                    DiagnosticLog.Info("RAWINPUT", "A raw-input device was disconnected.");
                    LogKeyboardDevices();
                }
            }

            // Never mark WM_INPUT handled: the OS still delivers the original key.
            return IntPtr.Zero;
        }

        private void ProcessRawInput(IntPtr rawInputHandle)
        {
            uint size = 0;
            uint headerSize = (uint)Marshal.SizeOf(typeof(RawInputHeader));
            uint result = GetRawInputData(
                rawInputHandle,
                RidInput,
                IntPtr.Zero,
                ref size,
                headerSize);

            if (result == UInt32.MaxValue || size < headerSize)
            {
                LogReadFailure("Could not query WM_INPUT data size.");
                return;
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                result = GetRawInputData(rawInputHandle, RidInput, buffer, ref size, headerSize);
                if (result == UInt32.MaxValue)
                {
                    LogReadFailure("GetRawInputData failed while copying WM_INPUT.");
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
                    if (!string.IsNullOrEmpty(devicePath) && _reportedOtherDevices.Add(devicePath))
                    {
                        DiagnosticLog.Trace(
                            "RAWINPUT",
                            "Ignoring non-K643 keyboard device: " + GetDeviceSummary(devicePath));
                    }
                    return;
                }

                IntPtr keyboardPointer = IntPtr.Add(buffer, (int)headerSize);
                var keyboard = (RawKeyboard)Marshal.PtrToStructure(
                    keyboardPointer,
                    typeof(RawKeyboard));

                bool isKeyDown = (keyboard.Flags & RiKeyBreak) == 0;
                bool isExtended = (keyboard.Flags & RiKeyE0) != 0;
                string keyIdentity = devicePath + "|"
                    + keyboard.VirtualKey.ToString("X2") + "|"
                    + keyboard.MakeCode.ToString("X2") + "|"
                    + (isExtended ? "E0" : "00");

                if (isKeyDown)
                {
                    // Suppress auto-repeat here: it adds noise and can resend paint actions.
                    if (!_pressedKeys.Add(keyIdentity))
                    {
                        return;
                    }
                }
                else
                {
                    _pressedKeys.Remove(keyIdentity);
                }

                DiagnosticLog.Input(string.Format(
                    "K643 {0}: VK=0x{1:X2}, scan=0x{2:X2}, E0={3}, device={4}",
                    isKeyDown ? "DOWN" : "UP",
                    keyboard.VirtualKey,
                    keyboard.MakeCode,
                    isExtended,
                    GetDeviceSummary(devicePath)));

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

        private void LogReadFailure(string message)
        {
            _readFailureCount++;
            if (_readFailureCount <= 3)
            {
                int errorCode = Marshal.GetLastWin32Error();
                DiagnosticLog.Warning(
                    "RAWINPUT",
                    message + " Win32 error " + errorCode + ": "
                        + new Win32Exception(errorCode).Message);
            }
            else if (_readFailureCount == 4)
            {
                DiagnosticLog.Warning("RAWINPUT", "Further WM_INPUT read errors are suppressed.");
            }
        }

        private void LogKeyboardDevices()
        {
            uint deviceCount = 0;
            uint listSize = (uint)Marshal.SizeOf(typeof(RawInputDeviceList));
            uint result = GetRawInputDeviceList(IntPtr.Zero, ref deviceCount, listSize);

            if (result == UInt32.MaxValue)
            {
                int errorCode = Marshal.GetLastWin32Error();
                DiagnosticLog.Warning(
                    "RAWINPUT",
                    "Could not enumerate raw-input devices. Win32 error " + errorCode + ".");
                return;
            }

            if (deviceCount == 0)
            {
                DiagnosticLog.Warning("RAWINPUT", "Windows reports no raw-input devices.");
                return;
            }

            if (deviceCount > 4096)
            {
                DiagnosticLog.Warning("RAWINPUT", "Raw-input device list is unexpectedly large: " + deviceCount + ".");
                return;
            }

            IntPtr buffer = Marshal.AllocHGlobal(checked((int)(deviceCount * listSize)));
            try
            {
                uint returnedCount = deviceCount;
                result = GetRawInputDeviceList(buffer, ref returnedCount, listSize);
                if (result == UInt32.MaxValue)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    DiagnosticLog.Warning(
                        "RAWINPUT",
                        "Could not read raw-input device list. Win32 error " + errorCode + ".");
                    return;
                }

                int keyboardCount = 0;
                int k643Count = 0;
                int structureSize = Marshal.SizeOf(typeof(RawInputDeviceList));

                for (int index = 0; index < returnedCount; index++)
                {
                    IntPtr itemPointer = IntPtr.Add(buffer, index * structureSize);
                    var item = (RawInputDeviceList)Marshal.PtrToStructure(
                        itemPointer,
                        typeof(RawInputDeviceList));

                    if (item.Type != RimTypeKeyboard)
                    {
                        continue;
                    }

                    keyboardCount++;
                    string path = GetDevicePath(item.Device);
                    if (IsVeigarK643(path))
                    {
                        k643Count++;
                        DiagnosticLog.Info(
                            "RAWINPUT",
                            "Detected K643 keyboard interface: " + GetDeviceSummary(path));
                    }
                    else if (!string.IsNullOrEmpty(path))
                    {
                        _reportedOtherDevices.Add(path);
                    }
                }

                if (k643Count == 0)
                {
                    DiagnosticLog.Warning(
                        "RAWINPUT",
                        "No keyboard with VID_258A PID_0049 was found. Check USB connection and the device filter.");
                }

                DiagnosticLog.Info(
                    "RAWINPUT",
                    "Raw keyboard inventory: " + keyboardCount
                        + " keyboard device(s), " + k643Count + " K643 interface(s).");
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

        private static string GetDeviceSummary(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
            {
                return "unknown device";
            }

            string[] parts = devicePath.Split('#');
            for (int index = 0; index < parts.Length; index++)
            {
                if (parts[index].IndexOf("VID_", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    parts[index].IndexOf("PID_", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return parts[index];
                }
            }

            return "K643";
        }

        private static string GetDevicePath(IntPtr deviceHandle)
        {
            uint characterCount = 0;
            uint result = GetRawInputDeviceInfo(
                deviceHandle,
                RidiDeviceName,
                IntPtr.Zero,
                ref characterCount);

            if (result == UInt32.MaxValue || characterCount == 0 || characterCount > 32768)
            {
                return string.Empty;
            }

            IntPtr nameBuffer = Marshal.AllocHGlobal(checked((int)(characterCount * 2)));
            try
            {
                uint size = characterCount;
                result = GetRawInputDeviceInfo(
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

                bool removed = RegisterRawInputDevices(
                    ref device,
                    1,
                    (uint)Marshal.SizeOf(typeof(RawInputDevice)));

                if (!removed)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    DiagnosticLog.Warning(
                        "RAWINPUT",
                        "Could not unregister Raw Input. Win32 error " + errorCode + ".");
                }
                else
                {
                    DiagnosticLog.Info("RAWINPUT", "Keyboard Raw Input unregistered.");
                }

                _registered = false;
            }

            _pressedKeys.Clear();
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

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputDeviceList
        {
            public IntPtr Device;
            public uint Type;
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceList(
            IntPtr rawInputDeviceList,
            ref uint numberOfDevices,
            uint size);
    }
}
