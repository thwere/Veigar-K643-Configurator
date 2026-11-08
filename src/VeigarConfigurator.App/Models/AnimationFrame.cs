using System.Collections.Generic;
using System.Runtime.Serialization;

namespace VeigarConfigurator.App.Models
{
    [DataContract]
    public sealed class AnimationFrame
    {
        public AnimationFrame()
        {
            DurationMs = 180;
            BaseColor = new RgbColor(255, 32, 32);
            SlotColors = new List<LedSlotColor>();
        }

        [DataMember(Order = 1)]
        public int DurationMs { get; set; }

        [DataMember(Order = 2)]
        public RgbColor BaseColor { get; set; }

        [DataMember(Order = 3)]
        public List<LedSlotColor> SlotColors { get; set; }
    }
}
