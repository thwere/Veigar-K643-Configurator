namespace VeigarConfigurator.Core.Devices
{
    public sealed class DeviceConnectionInfo
    {
        public DeviceConnectionInfo(bool isConnected, string title, string message, string devicePath)
        {
            IsConnected = isConnected;
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            DevicePath = devicePath ?? string.Empty;
        }

        public bool IsConnected { get; private set; }
        public string Title { get; private set; }
        public string Message { get; private set; }
        public string DevicePath { get; private set; }
    }
}
