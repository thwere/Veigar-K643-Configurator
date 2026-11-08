using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using VeigarConfigurator.App.Models;

namespace VeigarConfigurator.App.Profiles
{
    public static class LightingProfileStore
    {
        public static void Save(string path, LightingProfile profile)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A profile path is required.", "path");
            }

            if (profile == null)
            {
                throw new ArgumentNullException("profile");
            }

            var serializer = new DataContractJsonSerializer(typeof(LightingProfile));
            using (FileStream stream = File.Create(path))
            {
                serializer.WriteObject(stream, profile);
            }
        }

        public static LightingProfile Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A profile path is required.", "path");
            }

            var serializer = new DataContractJsonSerializer(typeof(LightingProfile));
            using (FileStream stream = File.OpenRead(path))
            {
                LightingProfile profile = serializer.ReadObject(stream) as LightingProfile;
                if (profile == null)
                {
                    throw new InvalidDataException("The selected file does not contain a lighting profile.");
                }

                if (profile.Layers == null)
                {
                    profile.Layers = new List<LightingLayer>();
                }

                if (profile.KeyMappings == null)
                {
                    profile.KeyMappings = new List<KeyLedMapping>();
                }

                // Migrate a v0.2.0-alpha profile into the new layer format.
                if (profile.Layers.Count == 0)
                {
                    var layer = new LightingLayer();
                    layer.Name = "Migrated base colour";
                    layer.Mode = "Static";
                    layer.StartColor = new RgbColor(
                        Clamp(profile.Red, 0, 255),
                        Clamp(profile.Green, 0, 255),
                        Clamp(profile.Blue, 0, 255));
                    profile.Layers.Add(layer);
                }

                return profile;
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
