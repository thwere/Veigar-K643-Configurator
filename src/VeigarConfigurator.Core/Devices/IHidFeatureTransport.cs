namespace VeigarConfigurator.Core.Devices
{
    public interface IHidFeatureTransport
    {
        bool TryOpen(string devicePath, out IHidFeatureSession session, out int errorCode);
    }
}
