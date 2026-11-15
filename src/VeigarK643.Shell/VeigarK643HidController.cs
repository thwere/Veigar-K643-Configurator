using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using System.Windows.Media;

namespace VeigarK643.Shell;

internal static class VeigarK643HidController
{
    private const int ReportId = 0x08;
    private const int LedSlotCount = 126;
    private const int ReportHeaderLength = 4;
    private const int ReportLength = ReportHeaderLength + LedSlotCount * 3;
    private static readonly SemaphoreSlim SendLock = new(1, 1);
    private static readonly Dictionary<string, int> Slots = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Escape"]=0, ["Oem3"]=1, ["Tab"]=2, ["Capital"]=3, ["LeftShift"]=4, ["LeftCtrl"]=5,
        ["D1"]=7, ["Q"]=8, ["A"]=9, ["Z"]=10, ["LWin"]=11, ["F1"]=12, ["D2"]=13, ["W"]=14,
        ["S"]=15, ["X"]=16, ["LeftAlt"]=17, ["F2"]=18, ["D3"]=19, ["E"]=20, ["D"]=21, ["C"]=22,
        ["F3"]=24, ["D4"]=25, ["R"]=26, ["F"]=27, ["V"]=28, ["F4"]=30, ["D5"]=31, ["T"]=32,
        ["G"]=33, ["B"]=34, ["Space"]=35, ["F5"]=36, ["D6"]=37, ["Y"]=38, ["H"]=39, ["N"]=40,
        ["F6"]=42, ["D7"]=43, ["U"]=44, ["J"]=45, ["M"]=46, ["F7"]=48, ["D8"]=49, ["I"]=50,
        ["K"]=51, ["OemComma"]=52, ["RightAlt"]=53, ["F8"]=54, ["D9"]=55, ["O"]=56, ["L"]=57,
        ["OemPeriod"]=58, ["RightCtrl"]=59, ["F9"]=60, ["D0"]=61, ["P"]=62, ["Oem1"]=63,
        ["OemQuestion"]=64, ["Fn"]=65, ["F10"]=66, ["OemMinus"]=67, ["OemOpenBrackets"]=68,
        ["OemQuotes"]=69, ["RightShift"]=70, ["F11"]=72, ["OemPlus"]=73, ["Oem6"]=74,
        ["Left"]=77, ["F12"]=78, ["Back"]=79, ["Oem5"]=80, ["Return"]=81, ["Up"]=82, ["Down"]=83,
        ["PrtSc"]=84, ["NumLock"]=85, ["NumPad7"]=86, ["NumPad4"]=87, ["NumPad1"]=88, ["Right"]=89,
        ["Subtract"]=90, ["Divide"]=91, ["NumPad8"]=92, ["NumPad5"]=93, ["NumPad2"]=94,
        ["NumPad0"]=95, ["Add"]=96, ["Multiply"]=97, ["NumPad9"]=98, ["NumPad6"]=99,
        ["NumPad3"]=100, ["Decimal"]=101
    };

    public static async Task SendColorsAsync(IReadOnlyDictionary<string, Color> colors)
    {
        byte[] report = new byte[ReportLength];
        report[0] = ReportId; report[1] = 0x0A; report[2] = 0x7A; report[3] = 0x01;
        foreach (KeyValuePair<string, Color> entry in colors)
        {
            if (!Slots.TryGetValue(entry.Key, out int slot))
                continue;
            int offset = 4 + slot * 3;
            report[offset] = entry.Value.R;
            report[offset + 1] = entry.Value.G;
            report[offset + 2] = entry.Value.B;
        }

        await SendLock.WaitAsync().ConfigureAwait(false);
        try { await Task.Run(() => SendFeatureReport(report)).ConfigureAwait(false); }
        finally { SendLock.Release(); }
    }

    private static void SendFeatureReport(byte[] report)
    {
        Guid hidGuid; HidD_GetHidGuid(out hidGuid);
        IntPtr set = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, 0x02 | 0x10);
        if (set == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error(), "HID enumeration failed.");
        string? path = null;
        try
        {
            for (uint i = 0; ; i++)
            {
                SpDeviceInterfaceData data = new() { Size = Marshal.SizeOf<SpDeviceInterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i, ref data))
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 259) break;
                    throw new Win32Exception(error, "HID interface enumeration failed.");
                }
                SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out uint required, IntPtr.Zero);
                // A Windows device-interface path is far smaller than this bound; reject malformed sizes before allocating unmanaged memory.
                if (required < 6 || required > 64 * 1024) continue;
                IntPtr buffer = Marshal.AllocHGlobal((int)required);
                try
                {
                    Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref data, buffer, required, out _, IntPtr.Zero)) continue;
                    string candidate = Marshal.PtrToStringUni(IntPtr.Add(buffer, 4)) ?? "";
                    if (!candidate.Contains("VID_258A", StringComparison.OrdinalIgnoreCase) ||
                        !candidate.Contains("PID_0049", StringComparison.OrdinalIgnoreCase) ||
                        !candidate.Contains("MI_01", StringComparison.OrdinalIgnoreCase) ||
                        !candidate.Contains("COL08", StringComparison.OrdinalIgnoreCase)) continue;
                    path = candidate; break;
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(set); }

        if (path is null) throw new InvalidOperationException("Veigar K643 HID interface MI_01 / Col08 was not found.");
        using SafeFileHandle handle = CreateFile(path, 0x80000000 | 0x40000000, 0x01 | 0x02, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to open Veigar K643 HID interface.");
        if (HidD_SetFeature(handle, report, report.Length) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "HidD_SetFeature failed.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData { public int Size; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

    [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid hidGuid);
    [DllImport("hid.dll", SetLastError=true)] private static extern byte HidD_SetFeature(SafeFileHandle handle, byte[] report, int length);
    [DllImport("setupapi.dll", SetLastError=true)] private static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr hwnd, uint flags);
    [DllImport("setupapi.dll", SetLastError=true)] private static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr deviceInfo, ref Guid guid, uint index, ref SpDeviceInterfaceData data);
    [DllImport("setupapi.dll", SetLastError=true, CharSet=CharSet.Unicode)] private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SpDeviceInterfaceData data, IntPtr detail, uint size, out uint required, IntPtr deviceInfo);
    [DllImport("setupapi.dll", SetLastError=true)] private static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", CharSet=CharSet.Auto, SetLastError=true)] private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
}
