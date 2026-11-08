using System.Runtime.Serialization;

namespace VeigarConfigurator.App.Models
{
    [DataContract]
    public sealed class KeyLedMapping
    {
        [DataMember(Order = 1)]
        public int VirtualKey { get; set; }

        [DataMember(Order = 2)]
        public int ScanCode { get; set; }

        [DataMember(Order = 3)]
        public int SlotIndex { get; set; }

        [DataMember(Order = 4)]
        public string KeyName { get; set; }
    }
}
