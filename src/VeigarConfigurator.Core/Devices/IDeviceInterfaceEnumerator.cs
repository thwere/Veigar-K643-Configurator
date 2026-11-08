using System.Collections.Generic;

namespace VeigarConfigurator.Core.Devices
{
    public interface IDeviceInterfaceEnumerator
    {
        IList<DeviceInterfaceInfo> FindInterfaces(ushort vendorId, ushort productId);
    }
}
