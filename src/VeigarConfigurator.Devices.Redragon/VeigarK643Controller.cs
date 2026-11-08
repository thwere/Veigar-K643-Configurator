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

            if (_session != null && string.Equals(
                _connectedPath, target.DevicePath, StringComparison.OrdinalIgnoreCase))
            {
                return new DeviceConnectionInfo(
                    true,
                    "Veigar K643 connected",
                    "USB VID 258A / PID 0049 · HID interface MI_01 / Col08",
                    _connectedPath);
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

            _session = newSession;
            _connectedPath = target.DevicePath;

            return new DeviceConnectionInfo(
                true,
                "Veigar K643 connected",
                "USB VID 258A / PID 0049 · HID interface MI_01 / Col08",
                _connectedPath);
        }

        public async Task SendSolidColorPreviewAsync(
            byte red,
            byte green,
            byte blue,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (_session == null)
            {
                throw new InvalidOperationException("The Veigar K643 is not connected.");
            }

            byte[] frame = VeigarRgbFrameBuilder.CreateSolidColorFrame(red, green, blue);

            // The proof-of-concept used 60 consecutive Feature Report writes over 3 seconds.
            for (int frameIndex = 0; frameIndex < 60; frameIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int errorCode;
                if (!_session.SetFeatureReport(frame, out errorCode))
                {
                    throw new System.ComponentModel.Win32Exception(
                        errorCode,
                        "HidD_SetFeature failed while sending RGB frame " + (frameIndex + 1) + ".");
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
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
            if (_session != null)
            {
                _session.Dispose();
                _session = null;
            }

            _connectedPath = null;
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
