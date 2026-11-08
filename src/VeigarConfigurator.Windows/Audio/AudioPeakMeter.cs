using System;
using System.Runtime.InteropServices;

namespace VeigarConfigurator.Windows.Audio
{
    /// <summary>
    /// Reads the Windows Core Audio peak meter for the default multimedia output.
    /// Audio samples are never captured or stored.
    /// </summary>
    public sealed class AudioPeakMeter : IDisposable
    {
        private IMMDeviceEnumerator _enumerator;
        private IMMDevice _device;
        private IAudioMeterInformation _meter;
        private bool _disposed;

        public AudioPeakMeter()
        {
            try
            {
                _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();

                int result = _enumerator.GetDefaultAudioEndpoint(0, 1, out _device);
                Marshal.ThrowExceptionForHR(result);

                Guid meterId = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");
                object meterObject;
                result = _device.Activate(
                    ref meterId,
                    23,
                    IntPtr.Zero,
                    out meterObject);
                Marshal.ThrowExceptionForHR(result);

                _meter = (IAudioMeterInformation)meterObject;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool TryGetPeakLevel(out float peakLevel)
        {
            peakLevel = 0.0f;
            if (_disposed || _meter == null)
            {
                return false;
            }

            int result = _meter.GetPeakValue(out peakLevel);
            if (result < 0)
            {
                peakLevel = 0.0f;
                return false;
            }

            if (peakLevel < 0.0f) peakLevel = 0.0f;
            if (peakLevel > 1.0f) peakLevel = 1.0f;
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ReleaseComObject(_meter);
            ReleaseComObject(_device);
            ReleaseComObject(_enumerator);
            _meter = null;
            _device = null;
            _enumerator = null;
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.ReleaseComObject(value);
            }
        }

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject
        {
        }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(int dataFlow, int stateMask, [MarshalAs(UnmanagedType.IUnknown)] out object devices);

            [PreserveSig]
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);

            [PreserveSig]
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

            [PreserveSig]
            int RegisterEndpointNotificationCallback(IntPtr client);

            [PreserveSig]
            int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(
                ref Guid interfaceId,
                int classContext,
                IntPtr activationParameters,
                [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);

            [PreserveSig]
            int OpenPropertyStore(int access, [MarshalAs(UnmanagedType.IUnknown)] out object properties);

            [PreserveSig]
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);

            [PreserveSig]
            int GetState(out int state);
        }

        [ComImport]
        [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioMeterInformation
        {
            [PreserveSig]
            int GetPeakValue(out float peak);

            [PreserveSig]
            int GetMeteringChannelCount(out int channelCount);

            [PreserveSig]
            int GetChannelsPeakValues(
                int channelCount,
                [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] float[] peaks);

            [PreserveSig]
            int QueryHardwareSupport(out int hardwareSupport);
        }
    }
}
