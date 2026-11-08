using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VeigarConfigurator.Core.Devices;
using VeigarConfigurator.Devices.Redragon.Protocol;

namespace VeigarConfigurator.Devices.Redragon
{
    public sealed class VeigarK643Controller : IDeviceController
    {
        public const ushort VendorId = 0x258A;
        public const ushort ProductId = 0x0049;
        public const int RequiredInterfaceNumber = 0x01;
        public const int RequiredCollectionNumber = 0x08;

        private readonly IDeviceInterfaceEnumerator _enumerator;
        private readonly IHidFeatureTransport _transport;
        private readonly object _sessionLock = new object();
        private IHidFeatureSession _session;
        private string _connectedPath;
        private bool _disposed;

        public VeigarK643Controller(
            IDeviceInterfaceEnumerator enumerator,
            IHidFeatureTransport transport)
        {
            _enumerator = enumerator;
            _transport = transport;
        }

        public DeviceConnectionInfo RefreshConnection()
        {
            ThrowIfDisposed();

            var interfaces = _enumerator.FindInterfaces(VendorId, ProductId);
            var target = interfaces.FirstOrDefault(
                item => item.InterfaceNumber == RequiredInterfaceNumber &&
                        item.CollectionNumber == RequiredCollectionNumber);

            if (target == null)
            {
                Disconnect();

                string message = interfaces.Count == 0
                    ? "Connect the Veigar K643 using USB. The application will check again automatically."
                    : "The keyboard was detected, but the expected HID interface MI_01 / Col08 was not found.";

                return new DeviceConnectionInfo(
                    false,
                    "Veigar K643 not connected",
                    message,
                    string.Empty);
            }

            lock (_sessionLock)
            {
                if (_session != null && string.Equals(
                    _connectedPath, target.DevicePath, StringComparison.OrdinalIgnoreCase))
                {
                    return new DeviceConnectionInfo(
                        true,
                        "Veigar K643 connected",
                        "USB VID 258A / PID 0049 · HID interface MI_01 / Col08",
                        _connectedPath);
                }
            }

            Disconnect();

            IHidFeatureSession newSession;
            int errorCode;
            if (!_transport.TryOpen(target.DevicePath, out newSession, out errorCode))
            {
                return new DeviceConnectionInfo(
                    false,
                    "Veigar K643 detected, but cannot be opened",
                    "Windows error " + errorCode + ". Close other keyboard configuration software and refresh.",
                    target.DevicePath);
            }

            lock (_sessionLock)
            {
                if (_disposed)
                {
                    newSession.Dispose();
                    throw new ObjectDisposedException("VeigarK643Controller");
                }

                _session = newSession;
                _connectedPath = target.DevicePath;
            }

            return new DeviceConnectionInfo(
                true,
                "Veigar K643 connected",
                "USB VID 258A / PID 0049 · HID interface MI_01 / Col08",
                target.DevicePath);
        }

        public Task SendRgbFrameAsync(byte[] slotRgbData, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            byte[] report = VeigarRgbFrameBuilder.CreateFrame(slotRgbData);

            // HidD_SetFeature is synchronous, so perform one write on a worker thread.
            return Task.Run(delegate
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (_sessionLock)
                {
                    ThrowIfDisposed();

                    if (_session == null)
                    {
                        throw new InvalidOperationException("The Veigar K643 is not connected.");
                    }

                    int errorCode;
                    if (!_session.SetFeatureReport(report, out errorCode))
                    {
                        throw new System.ComponentModel.Win32Exception(
                            errorCode,
                            "HidD_SetFeature failed while sending the RGB frame.");
                    }
                }
            }, cancellationToken);
        }

        public Task SendSolidColorPreviewAsync(
            byte red,
            byte green,
            byte blue,
            CancellationToken cancellationToken)
        {
            byte[] report = VeigarRgbFrameBuilder.CreateSolidColorFrame(red, green, blue);
            byte[] slotData = new byte[VeigarRgbFrameBuilder.SlotRgbDataLength];
            Buffer.BlockCopy(report, 4, slotData, 0, slotData.Length);
            return SendRgbFrameAsync(slotData, cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Disconnect();
        }

        private void Disconnect()
        {
            lock (_sessionLock)
            {
                if (_session != null)
                {
                    _session.Dispose();
                    _session = null;
                }

                _connectedPath = null;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("VeigarK643Controller");
            }
        }
    }
}
