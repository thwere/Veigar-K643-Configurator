using System;

namespace VeigarConfigurator.Devices.Redragon.Protocol
{
    public static class VeigarRgbFrameBuilder
    {
        public const int ReportId = 0x08;
        public const int ReportLength = 382;
        public const int LedSlotCount = 126;
        public const int SlotRgbDataLength = LedSlotCount * 3;
        private const int HeaderLength = 4;

        public static byte[] CreateSolidColorFrame(byte red, byte green, byte blue)
        {
            var slotData = new byte[SlotRgbDataLength];

            for (int slot = 0; slot < LedSlotCount; slot++)
            {
                int offset = slot * 3;
                slotData[offset] = red;
                slotData[offset + 1] = green;
                slotData[offset + 2] = blue;
            }

            return CreateFrame(slotData);
        }

        public static byte[] CreateFrame(byte[] slotRgbData)
        {
            if (slotRgbData == null)
            {
                throw new ArgumentNullException("slotRgbData");
            }

            if (slotRgbData.Length != SlotRgbDataLength)
            {
                throw new ArgumentException(
                    "RGB data must contain exactly " + SlotRgbDataLength + " bytes for " +
                    LedSlotCount + " LED slots.",
                    "slotRgbData");
            }

            var report = CreateEmptyFrame();
            Buffer.BlockCopy(slotRgbData, 0, report, HeaderLength, SlotRgbDataLength);
            return report;
        }

        public static byte[] CreateSingleSlotFrame(int slot, byte red, byte green, byte blue)
        {
            if (slot < 0 || slot >= LedSlotCount)
            {
                throw new ArgumentOutOfRangeException("slot");
            }

            var slotData = new byte[SlotRgbDataLength];
            int offset = slot * 3;
            slotData[offset] = red;
            slotData[offset + 1] = green;
            slotData[offset + 2] = blue;
            return CreateFrame(slotData);
        }

        private static byte[] CreateEmptyFrame()
        {
            var report = new byte[ReportLength];
            report[0] = ReportId;
            report[1] = 0x0A;
            report[2] = 0x7A;
            report[3] = 0x01;
            return report;
        }
    }
}
