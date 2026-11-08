using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using VeigarConfigurator.Core.Devices;

namespace VeigarConfigurator.Windows.Hid
{
    public sealed class HidDeviceEnumerator : IDeviceInterfaceEnumerator
    {
        private const uint DigcfPresent = 0x00000002;
        private const uint DigcfDeviceInterface = 0x00000010;
        private const int ErrorNoMoreItems = 259;

        public IList<DeviceInterfaceInfo> FindInterfaces(ushort vendorId, ushort productId)
        {
            var results = new List<DeviceInterfaceInfo>();
            Guid hidGuid;
            HidD_GetHidGuid(out hidGuid);

            IntPtr deviceInfoSet = SetupDiGetClassDevs(
                ref hidGuid, IntPtr.Zero, IntPtr.Zero, DigcfPresent | DigcfDeviceInterface);

            if (deviceInfoSet == new IntPtr(-1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to enumerate HID devices.");
            }

            try
            {
                uint index = 0;
                while (true)
                {
                    var interfaceData = new SpDeviceInterfaceData();
                    interfaceData.Size = Marshal.SizeOf(typeof(SpDeviceInterfaceData));

                    bool found = SetupDiEnumDeviceInterfaces(
                        deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData);

                    if (!found)
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == ErrorNoMoreItems)
                        {
                            break;
                        }

                        throw new Win32Exception(error, "Unable to read a HID interface.");
                    }

                    index++;

                    uint requiredSize;
                    SetupDiGetDeviceInterfaceDetail(
                        deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);

                    if (requiredSize < 6)
                    {
                        continue;
                    }

                    IntPtr detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
                    try
                    {
                        // SP_DEVICE_INTERFACE_DETAIL_DATA_W: cbSize is 8 on x64 and 6 on x86.
                        Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);

                        bool detailRead = SetupDiGetDeviceInterfaceDetail(
                            deviceInfoSet,
                            ref interfaceData,
                            detailBuffer,
                            requiredSize,
                            out requiredSize,
                            IntPtr.Zero);

                        if (!detailRead)
                        {
                            continue;
                        }

                        string path = Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, 4));
                        if (string.IsNullOrEmpty(path))
                        {
                            continue;
                        }

                        ushort pathVendor;
                        ushort pathProduct;
                        if (!TryReadHex(path, "VID_", out pathVendor) ||
                            !TryReadHex(path, "PID_", out pathProduct))
                        {
                            continue;
                        }

                        if (pathVendor != vendorId || pathProduct != productId)
                        {
                            continue;
                        }

                        results.Add(new DeviceInterfaceInfo(
                            path,
                            pathVendor,
                            pathProduct,
                            TryReadComponent(path, "MI_"),
                            TryReadComponent(path, "COL")));
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detailBuffer);
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return results;
        }

        private static bool TryReadHex(string value, string marker, out ushort result)
        {
            result = 0;
            Match match = Regex.Match(value, marker + "([0-9A-F]{4})", RegexOptions.IgnoreCase);
            return match.Success && ushort.TryParse(
                match.Groups[1].Value,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out result);
        }

        private static int? TryReadComponent(string value, string marker)
        {
            Match match = Regex.Match(value, marker + "([0-9A-F]{2})", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            int parsed;
            if (int.TryParse(
                match.Groups[1].Value,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out parsed))
            {
                return parsed;
            }

            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SpDeviceInterfaceData
        {
            public int Size;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [DllImport("hid.dll")]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid,
            IntPtr enumerator,
            IntPtr hwndParent,
            uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet,
            IntPtr deviceInfoData,
            ref Guid interfaceClassGuid,
            uint memberIndex,
            ref SpDeviceInterfaceData deviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet,
            ref SpDeviceInterfaceData deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            uint deviceInterfaceDetailDataSize,
            out uint requiredSize,
            IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);
    }
}
