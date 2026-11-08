using System;

namespace VeigarConfigurator.Core.Devices
{
    public interface IHidFeatureSession : IDisposable
    {
        bool SetFeatureReport(byte[] report, out int errorCode);
    }
}
