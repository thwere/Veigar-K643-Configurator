using System;
using System.Collections.Generic;

namespace VeigarConfigurator.App.Models
{
    /// <summary>
    /// Measured Redragon Veigar K643 slot-to-key table from Veigar_LED_map.csv.
    /// The source test sent 60 RGB reports per measured slot with zero send failures.
    /// A question mark in the source CSV is intentionally not assigned to a physical key.
    /// </summary>
    public static class VeigarK643LedMap
    {
        private static readonly Dictionary<string, int> KeyToSlot;
        private static readonly Dictionary<int, string> SlotToKey;

        static VeigarK643LedMap()
        {
            KeyToSlot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            SlotToKey = new Dictionary<int, string>();

            Add("ESC", 0); Add("GRAVE", 1); Add("TAB", 2); Add("CAPS", 3);
            Add("LSHIFT", 4); Add("LCTRL", 5);
            Add("D1", 7); Add("Q", 8); Add("A", 9); Add("Z", 10); Add("LWIN", 11);
            Add("F1", 12); Add("D2", 13); Add("W", 14); Add("S", 15); Add("X", 16); Add("LALT", 17);
            Add("F2", 18); Add("D3", 19); Add("E", 20); Add("D", 21); Add("C", 22);
            Add("F3", 24); Add("D4", 25); Add("R", 26); Add("F", 27); Add("V", 28);
            Add("F4", 30); Add("D5", 31); Add("T", 32); Add("G", 33); Add("B", 34); Add("SPACE", 35);
            Add("F5", 36); Add("D6", 37); Add("Y", 38); Add("H", 39); Add("N", 40);
            Add("F6", 42); Add("D7", 43); Add("U", 44); Add("J", 45); Add("M", 46);
            Add("F7", 48); Add("D8", 49); Add("I", 50); Add("K", 51); Add("COMMA", 52); Add("RALT", 53);
            Add("F8", 54); Add("D9", 55); Add("O", 56); Add("L", 57); Add("PERIOD", 58); Add("RCTRL", 59);
            Add("F9", 60); Add("D0", 61); Add("P", 62); Add("SEMICOLON", 63); Add("SLASH", 64); Add("FN", 65);
            Add("F10", 66); Add("MINUS", 67); Add("LB", 68); Add("QUOTE", 69); Add("RSHIFT", 70);
            Add("F11", 72); Add("EQUAL", 73); Add("RB", 74);
            Add("LEFT", 77); Add("F12", 78); Add("BACKSPACE", 79); Add("BSLASH", 80);
            Add("ENTER", 81); Add("UP", 82); Add("DOWN", 83); Add("PRTSC", 84); Add("NUM", 85);
            Add("NUM7", 86); Add("NUM4", 87); Add("NUM1", 88); Add("RIGHT", 89);
            Add("FNMINUS", 90); Add("NUMDIV", 91); Add("NUM8", 92); Add("NUM5", 93); Add("NUM2", 94);
            Add("NUM0", 95); Add("FNPLUS", 96); Add("NUMMUL", 97); Add("NUM9", 98);
            Add("NUM6", 99); Add("NUM3", 100); Add("NUMDOT", 101);
        }

        public static int Count
        {
            get { return KeyToSlot.Count; }
        }

        public static bool TryGetSlot(string keyId, out int slot)
        {
            if (string.IsNullOrEmpty(keyId))
            {
                slot = -1;
                return false;
            }

            return KeyToSlot.TryGetValue(keyId, out slot);
        }

        public static bool TryGetKeyId(int slot, out string keyId)
        {
            return SlotToKey.TryGetValue(slot, out keyId);
        }

        private static void Add(string keyId, int slot)
        {
            KeyToSlot.Add(keyId, slot);
            SlotToKey.Add(slot, keyId);
        }
    }
}
