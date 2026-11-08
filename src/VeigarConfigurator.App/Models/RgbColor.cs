using System.Runtime.Serialization;

namespace VeigarConfigurator.App.Models
{
    [DataContract]
    public sealed class RgbColor
    {
        public RgbColor()
        {
        }

        public RgbColor(int red, int green, int blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }

        [DataMember(Order = 1)]
        public int Red { get; set; }

        [DataMember(Order = 2)]
        public int Green { get; set; }

        [DataMember(Order = 3)]
        public int Blue { get; set; }

        public RgbColor Copy()
        {
            return new RgbColor(Red, Green, Blue);
        }
    }
}
