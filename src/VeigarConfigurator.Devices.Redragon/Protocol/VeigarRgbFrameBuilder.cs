namespace VeigarConfigurator.Devices.Redragon.Protocol
{
    public static class VeigarRgbFrameBuilder
    {
        public const int ReportId = 0x08;
        public const int ReportLength = 382;
        public const int LedSlotCount = 126;
        private const int HeaderLength = 4;

        public static byte[] CreateSolidColorFrame(byte red, byte green, byte blue)
        {
            var report = CreateEmptyFrame();

            for (int slot = 0; slot < LedSlotCount; slot++)
            {
                SetSlotColor(report, slot, red, green, blue);
            }

            return report;
        }

        public static byte[] CreateSingleSlotFrame(int slot, byte red, byte green, byte blue)
        {
            var report = CreateEmptyFrame();
            SetSlotColor(report, slot, red, green, blue);
            return report;
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

        private static void SetSlotColor(byte[] report, int slot, byte red, byte green, byte blue)
        {
            if (slot < 0 || slot >= LedSlotCount)
            {
                throw new System.ArgumentOutOfRangeException("slot");
            }

            int offset = HeaderLength + (slot * 3);
            report[offset] = red;
            report[offset + 1] = green;
            report[offset + 2] = blue;
        }
    }
}
