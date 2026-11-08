using System.Runtime.Serialization;

namespace VeigarConfigurator.App.Models
{
    [DataContract]
    public sealed class LedSlotColor
    {
        [DataMember(Order = 1)]
        public int SlotIndex { get; set; }

        [DataMember(Order = 2)]
        public RgbColor Color { get; set; }
    }
}
