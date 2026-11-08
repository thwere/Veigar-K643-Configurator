using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using VeigarConfigurator.Core.Devices;

namespace VeigarConfigurator.Windows.Hid
{
    public sealed class HidFeatureTransport : IHidFeatureTransport
    {
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint ShareRead = 0x00000001;
        private const uint ShareWrite = 0x00000002;
        private const uint OpenExisting = 3;

        public bool TryOpen(string devicePath, out IHidFeatureSession session, out int errorCode)
        {
            session = null;
            errorCode = 0;

            SafeFileHandle handle = CreateFile(
                devicePath,
                GenericRead | GenericWrite,
                ShareRead | ShareWrite,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);

            if (handle == null || handle.IsInvalid)
            {
                errorCode = Marshal.GetLastWin32Error();
                if (handle != null)
                {
                    handle.Dispose();
                }

                return false;
            }

            session = new HidFeatureSession(handle);
            return true;
        }

        private sealed class HidFeatureSession : IHidFeatureSession
        {
            private readonly SafeFileHandle _handle;
            private bool _disposed;

            public HidFeatureSession(SafeFileHandle handle)
            {
                _handle = handle;
            }

            public bool SetFeatureReport(byte[] report, out int errorCode)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException("HidFeatureSession");
                }

                if (report == null || report.Length == 0)
                {
                    throw new ArgumentException("A non-empty HID report is required.", "report");
                }

                bool result = HidD_SetFeature(_handle, report, report.Length);
                errorCode = result ? 0 : Marshal.GetLastWin32Error();
                return result;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _handle.Dispose();
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool HidD_SetFeature(
            SafeFileHandle hidDeviceObject,
            byte[] reportBuffer,
            int reportBufferLength);
    }
}
