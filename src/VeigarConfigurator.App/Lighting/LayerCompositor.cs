using System;
using System.Collections.Generic;
using VeigarConfigurator.App.Models;

namespace VeigarConfigurator.App.Lighting
{
    public static class LayerCompositor
    {
        public static byte[] Render(
            IList<LightingLayer> layers,
            double audioLevel,
            IDictionary<int, double> keyIntensities,
            double elapsedMilliseconds)
        {
            const int slotCount = 126;
            var output = new byte[slotCount * 3];

            if (layers == null)
            {
                return output;
            }

            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                LightingLayer layer = layers[layerIndex];
                if (layer == null || !layer.IsEnabled)
                {
                    continue;
                }

                for (int slot = 0; slot < slotCount; slot++)
                {
                    RgbColor color = GetLayerColor(
                        layer,
                        slot,
                        audioLevel,
                        keyIntensities,
                        elapsedMilliseconds);

                    int offset = slot * 3;
                    Blend(
                        output,
                        offset,
                        color,
                        layer.Opacity,
                        layer.BlendMode);
                }
            }

            return output;
        }

        private static RgbColor GetLayerColor(
            LightingLayer layer,
            int slot,
            double audioLevel,
            IDictionary<int, double> keyIntensities,
            double elapsedMilliseconds)
        {
            RgbColor start = layer.StartColor ?? new RgbColor(0, 0, 0);
            RgbColor end = layer.EndColor ?? start;

            if (layer.Mode == "Gradient")
            {
                double position = slot / 125.0;
                return Interpolate(start, end, position);
            }

            if (layer.Mode == "Animation")
            {
                return GetAnimationColor(layer, slot, elapsedMilliseconds);
            }

            if (layer.Mode == "AudioReactive")
            {
                double level = Clamp(audioLevel, 0.0, 1.0);
                return Scale(start, 0.08 + (0.92 * level));
            }

            if (layer.Mode == "KeyReactive")
            {
                double intensity = 0.0;
                if (keyIntensities != null)
                {
                    keyIntensities.TryGetValue(slot, out intensity);
                }

                return Scale(start, intensity);
            }

            RgbColor slotColor = FindSlotColor(layer.SlotColors, slot);
            return slotColor ?? start;
        }

        private static RgbColor GetAnimationColor(
            LightingLayer layer,
            int slot,
            double elapsedMilliseconds)
        {
            if (layer.Frames == null || layer.Frames.Count == 0)
            {
                return layer.StartColor ?? new RgbColor(0, 0, 0);
            }

            int totalDuration = 0;
            for (int index = 0; index < layer.Frames.Count; index++)
            {
                totalDuration += Math.Max(20, layer.Frames[index].DurationMs);
            }

            if (totalDuration <= 0)
            {
                return layer.Frames[0].BaseColor ?? layer.StartColor;
            }

            double position = elapsedMilliseconds % totalDuration;
            AnimationFrame selected = layer.Frames[0];

            for (int index = 0; index < layer.Frames.Count; index++)
            {
                AnimationFrame frame = layer.Frames[index];
                int duration = Math.Max(20, frame.DurationMs);
                if (position < duration)
                {
                    selected = frame;
                    break;
                }

                position -= duration;
            }

            RgbColor slotColor = FindSlotColor(selected.SlotColors, slot);
            if (slotColor != null)
            {
                return slotColor;
            }

            return selected.BaseColor ?? layer.StartColor ?? new RgbColor(0, 0, 0);
        }

        private static RgbColor FindSlotColor(IList<LedSlotColor> colors, int slot)
        {
            if (colors == null)
            {
                return null;
            }

            for (int index = 0; index < colors.Count; index++)
            {
                if (colors[index] != null && colors[index].SlotIndex == slot)
                {
                    return colors[index].Color;
                }
            }

            return null;
        }

        private static void Blend(
            byte[] target,
            int offset,
            RgbColor source,
            int opacity,
            string blendMode)
        {
            double alpha = Clamp(opacity / 100.0, 0.0, 1.0);
            int red = ClampChannel(source.Red);
            int green = ClampChannel(source.Green);
            int blue = ClampChannel(source.Blue);

            if (blendMode == "Add")
            {
                target[offset] = (byte)Math.Min(255, target[offset] + (red * alpha));
                target[offset + 1] = (byte)Math.Min(255, target[offset + 1] + (green * alpha));
                target[offset + 2] = (byte)Math.Min(255, target[offset + 2] + (blue * alpha));
                return;
            }

            if (blendMode == "Max")
            {
                target[offset] = (byte)Math.Max(target[offset], red * alpha);
                target[offset + 1] = (byte)Math.Max(target[offset + 1], green * alpha);
                target[offset + 2] = (byte)Math.Max(target[offset + 2], blue * alpha);
                return;
            }

            target[offset] = (byte)Math.Round(target[offset] * (1.0 - alpha) + red * alpha);
            target[offset + 1] = (byte)Math.Round(target[offset + 1] * (1.0 - alpha) + green * alpha);
            target[offset + 2] = (byte)Math.Round(target[offset + 2] * (1.0 - alpha) + blue * alpha);
        }

        private static RgbColor Interpolate(RgbColor first, RgbColor second, double amount)
        {
            double ratio = Clamp(amount, 0.0, 1.0);
            return new RgbColor(
                (int)Math.Round(first.Red + ((second.Red - first.Red) * ratio)),
                (int)Math.Round(first.Green + ((second.Green - first.Green) * ratio)),
                (int)Math.Round(first.Blue + ((second.Blue - first.Blue) * ratio)));
        }

        private static RgbColor Scale(RgbColor color, double factor)
        {
            double value = Clamp(factor, 0.0, 1.0);
            return new RgbColor(
                (int)Math.Round(ClampChannel(color.Red) * value),
                (int)Math.Round(ClampChannel(color.Green) * value),
                (int)Math.Round(ClampChannel(color.Blue) * value));
        }

        private static int ClampChannel(int value)
        {
            return (int)Clamp(value, 0.0, 255.0);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
