using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

                // Restore collection defaults for fields omitted by older or hand-edited JSON.
                for (int index = 0; index < profile.Layers.Count; index++)
                {
                    LightingLayer layer = profile.Layers[index];
                    if (layer == null)
                    {
                        continue;
                    }

                    if (layer.SlotColors == null) layer.SlotColors = new List<LedSlotColor>();
                    if (layer.Frames == null) layer.Frames = new List<AnimationFrame>();
                    if (layer.StartColor == null) layer.StartColor = new RgbColor(255, 32, 32);
                    if (layer.EndColor == null) layer.EndColor = layer.StartColor.Copy();

                    for (int frameIndex = 0; frameIndex < layer.Frames.Count; frameIndex++)
                    {
                        AnimationFrame frame = layer.Frames[frameIndex];
                        if (frame == null) continue;
                        if (frame.SlotColors == null) frame.SlotColors = new List<LedSlotColor>();
                        if (frame.BaseColor == null) frame.BaseColor = layer.StartColor.Copy();
                        if (frame.DurationMs < 20) frame.DurationMs = 180;
                    }
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

        public static void SaveAnimation(string path, string name, IList<LightingLayer> layers)
        {
            if (layers == null) throw new ArgumentNullException("layers");
            List<LightingLayer> animationLayers = layers
                .Where(layer => layer != null && layer.Mode == "Animation")
                .ToList();

            if (animationLayers.Count == 0)
                throw new InvalidDataException("There are no animation layers to save.");

            var project = new LightingProfile
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Veigar animation" : name.Trim(),
                Layers = animationLayers,
                KeyMappings = new List<KeyLedMapping>()
            };

            Save(path, project);
        }

        public static IList<LightingLayer> LoadAnimation(string path)
        {
            LightingProfile project = Load(path);
            List<LightingLayer> animationLayers = project.Layers
                .Where(layer => layer != null && layer.Mode == "Animation")
                .ToList();

            if (animationLayers.Count == 0)
                throw new InvalidDataException("The selected .vca file does not contain animation layers.");

            return animationLayers;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
