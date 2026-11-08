namespace VeigarConfigurator.Core.Devices
{
    public sealed class DeviceInterfaceInfo
    {
        public DeviceInterfaceInfo(
            string devicePath,
            ushort vendorId,
            ushort productId,
            int? interfaceNumber,
            int? collectionNumber)
        {
            DevicePath = devicePath;
            VendorId = vendorId;
            ProductId = productId;
            InterfaceNumber = interfaceNumber;
            CollectionNumber = collectionNumber;
        }

        public string DevicePath { get; private set; }
        public ushort VendorId { get; private set; }
        public ushort ProductId { get; private set; }
        public int? InterfaceNumber { get; private set; }
        public int? CollectionNumber { get; private set; }
    }
}
