using System.Runtime.Serialization;

namespace VeigarConfigurator.App.Profiles
{
    [DataContract]
    public sealed class LightingProfile
    {
        [DataMember(Order = 1)]
        public string Name { get; set; }

        [DataMember(Order = 2)]
        public int Red { get; set; }

        [DataMember(Order = 3)]
        public int Green { get; set; }

        [DataMember(Order = 4)]
        public int Blue { get; set; }

        [DataMember(Order = 5)]
        public int Brightness { get; set; }
    }
}
