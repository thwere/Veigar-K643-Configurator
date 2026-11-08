using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;

namespace VeigarConfigurator.App.ViewModels
{
    public sealed class KeyboardKeyViewModel : INotifyPropertyChanged
    {
        private Brush _brush;
        private Brush _borderBrush;
        private bool _isSelected;
        private bool _isPhysicallyPressed;
        private string _mappingName;

        public KeyboardKeyViewModel(
            string keyId, string label, int virtualKeyCode, int scanCodeHint,
            double x, double y, double width, double height, bool isExtendedHint)
        {
            KeyId = keyId;
            Label = label;
            VirtualKeyCode = virtualKeyCode;
            ScanCodeHint = scanCodeHint;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            IsExtendedHint = isExtendedHint;
            _brush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            _borderBrush = new SolidColorBrush(Color.FromRgb(90, 103, 117));
            _mappingName = "Measured LED map";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string KeyId { get; private set; }
        public string Label { get; private set; }
        public int VirtualKeyCode { get; private set; }
        public int ScanCodeHint { get; private set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }
        public bool IsExtendedHint { get; private set; }

        public Brush Brush
        {
            get { return _brush; }
            set { SetField(ref _brush, value, "Brush"); }
        }

        public Brush BorderBrush
        {
            get { return _borderBrush; }
            set { SetField(ref _borderBrush, value, "BorderBrush"); }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetField(ref _isSelected, value, "IsSelected"); }
        }

        public bool IsPhysicallyPressed
        {
            get { return _isPhysicallyPressed; }
            set { SetField(ref _isPhysicallyPressed, value, "IsPhysicallyPressed"); }
        }

        public string MappingName
        {
            get { return _mappingName; }
            set { SetField(ref _mappingName, value, "MappingName"); }
        }

        private void SetField<T>(ref T field, T value, string propertyName)
        {
            if (object.Equals(field, value)) return;
            field = value;
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Hit zones are measured against the supplied 1198x444 keyboard reference image.
    /// The rendered image is 1000x371, preserving the original aspect ratio.
    /// </summary>
    public static class KeyboardLayoutBuilder
    {
        private const double Scale = 1000.0 / 1198.0;

        public static ObservableCollection<KeyboardKeyViewModel> Create()
        {
            var keys = new ObservableCollection<KeyboardKeyViewModel>();

            // Function row.
            Add(keys, "ESC", "Esc", 0x1B, 0, false, 20, 20, 65, 65);
            Add(keys, "F1", "F1", 0x70, 0, false, 145, 20, 65, 65);
            Add(keys, "F2", "F2", 0x71, 0, false, 212, 20, 65, 65);
            Add(keys, "F3", "F3", 0x72, 0, false, 278, 20, 65, 65);
            Add(keys, "F4", "F4", 0x73, 0, false, 345, 20, 65, 65);
            Add(keys, "F5", "F5", 0x74, 0, false, 430, 20, 65, 65);
            Add(keys, "F6", "F6", 0x75, 0, false, 498, 20, 65, 65);
            Add(keys, "F7", "F7", 0x76, 0, false, 565, 20, 65, 65);
            Add(keys, "F8", "F8", 0x77, 0, false, 634, 20, 65, 65);
            Add(keys, "F9", "F9", 0x78, 0, false, 718, 20, 65, 65);
            Add(keys, "F10", "F10", 0x79, 0, false, 786, 20, 65, 65);
            Add(keys, "F11", "F11", 0x7A, 0, false, 852, 20, 65, 65);
            Add(keys, "F12", "F12", 0x7B, 0, false, 919, 20, 65, 65);
            Add(keys, "PRTSC", "PrtSc", 0x2C, 0, true, 984, 20, 66, 65);
            Add(keys, "FNMINUS", "−", 0x6D, 0x4A, false, 1052, 20, 66, 65);
            Add(keys, "FNPLUS", "+", 0x6B, 0x4E, false, 1119, 20, 65, 65);

            // Number row and the three-key top row of the numeric block.
            Add(keys, "GRAVE", "~", 0xC0, 0, false, 20, 94, 63, 58);
            Add(keys, "D1", "1", 0x31, 0, false, 86, 94, 65, 58);
            Add(keys, "D2", "2", 0x32, 0, false, 153, 94, 66, 58);
            Add(keys, "D3", "3", 0x33, 0, false, 219, 94, 67, 58);
            Add(keys, "D4", "4", 0x34, 0, false, 287, 94, 66, 58);
            Add(keys, "D5", "5", 0x35, 0, false, 355, 94, 67, 58);
            Add(keys, "D6", "6", 0x36, 0, false, 423, 94, 66, 58);
            Add(keys, "D7", "7", 0x37, 0, false, 490, 94, 66, 58);
            Add(keys, "D8", "8", 0x38, 0, false, 557, 94, 66, 58);
            Add(keys, "D9", "9", 0x39, 0, false, 624, 94, 65, 58);
            Add(keys, "D0", "0", 0x30, 0, false, 691, 94, 65, 58);
            Add(keys, "MINUS", "−", 0xBD, 0, false, 757, 94, 66, 58);
            Add(keys, "EQUAL", "=", 0xBB, 0, false, 824, 94, 65, 58);
            Add(keys, "BACKSPACE", "Backspace", 0x08, 0, false, 891, 94, 90, 58);
            Add(keys, "NUM", "Num", 0x90, 0, false, 982, 94, 67, 58);
            Add(keys, "NUMDIV", "/", 0x6F, 0, true, 1050, 94, 66, 58);
            Add(keys, "NUMMUL", "*", 0x6A, 0, false, 1117, 94, 67, 58);

            // QWERTY row and Num 7/8/9.
            Add(keys, "TAB", "Tab", 0x09, 0, false, 20, 159, 95, 61);
            AddLetter(keys, "Q", 0x51, 116, 159);
            AddLetter(keys, "W", 0x57, 183, 159);
            AddLetter(keys, "E", 0x45, 250, 159);
            AddLetter(keys, "R", 0x52, 317, 159);
            AddLetter(keys, "T", 0x54, 384, 159);
            AddLetter(keys, "Y", 0x59, 451, 159);
            AddLetter(keys, "U", 0x55, 518, 159);
            AddLetter(keys, "I", 0x49, 585, 159);
            AddLetter(keys, "O", 0x4F, 652, 159);
            AddLetter(keys, "P", 0x50, 719, 159);
            Add(keys, "LB", "[", 0xDB, 0, false, 786, 159, 65, 61);
            Add(keys, "RB", "]", 0xDD, 0, false, 852, 159, 66, 61);
            Add(keys, "BSLASH", "\\", 0xDC, 0, false, 919, 159, 63, 61);
            Add(keys, "NUM7", "7", 0x67, 0, false, 983, 159, 66, 61);
            Add(keys, "NUM8", "8", 0x68, 0, false, 1050, 159, 66, 61);
            Add(keys, "NUM9", "9", 0x69, 0, false, 1117, 159, 66, 61);

            // Home row and Num 4/5/6.
            Add(keys, "CAPS", "CapsLk", 0x14, 0, false, 20, 223, 112, 59);
            AddLetter(keys, "A", 0x41, 135, 223);
            AddLetter(keys, "S", 0x53, 202, 223);
            AddLetter(keys, "D", 0x44, 269, 223);
            AddLetter(keys, "F", 0x46, 336, 223);
            AddLetter(keys, "G", 0x47, 403, 223);
            AddLetter(keys, "H", 0x48, 469, 223);
            AddLetter(keys, "J", 0x4A, 536, 223);
            AddLetter(keys, "K", 0x4B, 603, 223);
            AddLetter(keys, "L", 0x4C, 670, 223);
            Add(keys, "SEMICOLON", ";", 0xBA, 0, false, 737, 223, 66, 59);
            Add(keys, "QUOTE", "'", 0xDE, 0, false, 804, 223, 66, 59);
            Add(keys, "ENTER", "Enter", 0x0D, 0, false, 871, 223, 110, 59);
            Add(keys, "NUM4", "4", 0x64, 0, false, 982, 223, 67, 59);
            Add(keys, "NUM5", "5", 0x65, 0, false, 1050, 223, 66, 59);
            Add(keys, "NUM6", "6", 0x66, 0, false, 1117, 223, 67, 59);

            // Shift row and navigation/numeric row.
            Add(keys, "LSHIFT", "Shift", 0x10, 0x2A, false, 20, 286, 150, 58);
            AddLetter(keys, "Z", 0x5A, 172, 286);
            AddLetter(keys, "X", 0x58, 238, 286);
            AddLetter(keys, "C", 0x43, 305, 286);
            AddLetter(keys, "V", 0x56, 371, 286);
            AddLetter(keys, "B", 0x42, 438, 286);
            AddLetter(keys, "N", 0x4E, 504, 286);
            AddLetter(keys, "M", 0x4D, 571, 286);
            Add(keys, "COMMA", ",", 0xBC, 0, false, 638, 286, 66, 58);
            Add(keys, "PERIOD", ".", 0xBE, 0, false, 705, 286, 65, 58);
            Add(keys, "SLASH", "/", 0xBF, 0, false, 771, 286, 66, 58);
            Add(keys, "RSHIFT", "Shift", 0x10, 0x36, false, 838, 286, 75, 58);
            Add(keys, "UP", "↑", 0x26, 0, true, 915, 286, 66, 58);
            Add(keys, "NUM1", "1", 0x61, 0, false, 983, 286, 66, 58);
            Add(keys, "NUM2", "2", 0x62, 0, false, 1050, 286, 66, 58);
            Add(keys, "NUM3", "3", 0x63, 0, false, 1117, 286, 66, 58);

            // Bottom row. This keyboard has no separate NumPad Enter or Menu key.
            Add(keys, "LCTRL", "Ctrl", 0x11, 0, false, 20, 350, 82, 58);
            Add(keys, "LWIN", "Win", 0x5B, 0, true, 103, 350, 76, 58);
            Add(keys, "LALT", "Alt", 0x12, 0, false, 180, 350, 78, 58);
            Add(keys, "SPACE", "Space", 0x20, 0, false, 259, 350, 395, 58);
            Add(keys, "RALT", "Alt", 0x12, 0, true, 655, 350, 67, 58);
            Add(keys, "RCTRL", "Ctrl", 0x11, 0, true, 723, 350, 67, 58);
            Add(keys, "FN", "Fn", 0x00, 0, false, 791, 350, 67, 58);
            Add(keys, "LEFT", "←", 0x25, 0, true, 859, 350, 65, 58);
            Add(keys, "DOWN", "↓", 0x28, 0, true, 925, 350, 65, 58);
            Add(keys, "RIGHT", "→", 0x27, 0, true, 991, 350, 67, 58);
            Add(keys, "NUM0", "0", 0x60, 0, false, 1059, 350, 67, 58);
            Add(keys, "NUMDOT", ".", 0x6E, 0, false, 1127, 350, 57, 58);

            return keys;
        }

        public static string KeyIdFromInput(int virtualKey, int scanCode, bool isExtended)
        {
            if (virtualKey == 0x10) return scanCode == 0x36 ? "RSHIFT" : "LSHIFT";
            if (virtualKey == 0x11) return isExtended ? "RCTRL" : "LCTRL";
            if (virtualKey == 0x12) return isExtended ? "RALT" : "LALT";
            if (virtualKey == 0x0D) return "ENTER";
            if (virtualKey == 0x5B || virtualKey == 0x5C) return "LWIN";

            // Navigation keys share virtual-key codes with the numeric block when NumLock is off.
            if (virtualKey == 0x24) return isExtended ? string.Empty : "NUM7";   // Home / Num 7
            if (virtualKey == 0x21) return isExtended ? string.Empty : "NUM9";   // Page Up / Num 9
            if (virtualKey == 0x23) return isExtended ? string.Empty : "NUM1";   // End / Num 1
            if (virtualKey == 0x22) return isExtended ? string.Empty : "NUM3";   // Page Down / Num 3
            if (virtualKey == 0x2D) return isExtended ? string.Empty : "NUM0";   // Insert / Num 0
            if (virtualKey == 0x2E) return isExtended ? string.Empty : "NUMDOT"; // Delete / Num decimal
            if (virtualKey == 0x26) return isExtended ? "UP" : "NUM8";
            if (virtualKey == 0x25) return isExtended ? "LEFT" : "NUM4";
            if (virtualKey == 0x28) return isExtended ? "DOWN" : "NUM2";
            if (virtualKey == 0x27) return isExtended ? "RIGHT" : "NUM6";

            KeyboardKeyViewModel key = Create().FirstOrDefault(
                item => item.VirtualKeyCode == virtualKey &&
                        item.IsExtendedHint == isExtended &&
                        (item.ScanCodeHint == 0 || item.ScanCodeHint == scanCode));
            return key == null ? string.Empty : key.KeyId;
        }

        private static void AddLetter(
            ObservableCollection<KeyboardKeyViewModel> keys,
            string id, int virtualKey, double x, double y)
        {
            Add(keys, id, id, virtualKey, 0, false, x, y, 66, 61);
        }

        private static void Add(
            ObservableCollection<KeyboardKeyViewModel> keys,
            string id, string label, int virtualKey, int scanCode, bool isExtended,
            double x, double y, double width, double height)
        {
            // Inset the hit zone slightly to preserve the original key-cap border and legends.
            double insetX = id == "SPACE" ? 4 : 3;
            double insetY = 4;
            keys.Add(new KeyboardKeyViewModel(
                id, label, virtualKey, scanCode,
                (x + insetX) * Scale,
                (y + insetY) * Scale,
                (width - insetX * 2) * Scale,
                (height - insetY * 2) * Scale,
                isExtended));
        }
    }
}
