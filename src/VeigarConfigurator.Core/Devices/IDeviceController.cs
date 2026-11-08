using System;
using System.Threading;
using System.Threading.Tasks;

namespace VeigarConfigurator.Core.Devices
{
    public interface IDeviceController : IDisposable
    {
        DeviceConnectionInfo RefreshConnection();

        Task SendRgbFrameAsync(byte[] slotRgbData, CancellationToken cancellationToken);

        Task SendSolidColorPreviewAsync(
            byte red,
            byte green,
            byte blue,
            CancellationToken cancellationToken);
    }
}
