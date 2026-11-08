using System.Collections.Generic;
using System.Runtime.Serialization;
using VeigarConfigurator.App.Models;

namespace VeigarConfigurator.App.Profiles
{
    [DataContract]
    public sealed class LightingProfile
    {
        public LightingProfile()
        {
            Layers = new List<LightingLayer>();
            KeyMappings = new List<KeyLedMapping>();
            Brightness = 100;
        }

        [DataMember(Order = 1)]
        public string Name { get; set; }

        // Kept for backward compatibility with v0.2.0-alpha JSON profiles.
        [DataMember(Order = 2)]
        public int Red { get; set; }

        [DataMember(Order = 3)]
        public int Green { get; set; }

        [DataMember(Order = 4)]
        public int Blue { get; set; }

        [DataMember(Order = 5)]
        public int Brightness { get; set; }

        [DataMember(Order = 6)]
        public List<LightingLayer> Layers { get; set; }

        [DataMember(Order = 7)]
        public List<KeyLedMapping> KeyMappings { get; set; }
    }
}
