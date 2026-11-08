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
            _brush = new SolidColorBrush(Color.FromRgb(54, 62, 73));
            _borderBrush = new SolidColorBrush(Color.FromRgb(83, 95, 110));
            _mappingName = "Not calibrated";
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

    public static class KeyboardLayoutBuilder
    {
        private const double Unit = 38.0;
        private const double Gap = 5.0;
        private const double KeyHeight = 42.0;
        private const double RowGap = 8.0;

        private sealed class KeySpec
        {
            public string Id;
            public string Label;
            public int VirtualKey;
            public int ScanCode;
            public double Width;
            public double GapBefore;
            public double Height;
            public bool IsExtended;

            public KeySpec(string id, string label, int vk, double width, double gapBefore)
            {
                Id = id; Label = label; VirtualKey = vk; Width = width; GapBefore = gapBefore;
                Height = KeyHeight;
                IsExtended = false;
            }

            public KeySpec(string id, string label, int vk, double width, double gapBefore, double height, bool isExtended)
                : this(id, label, vk, width, gapBefore)
            {
                Height = height;
                IsExtended = isExtended;
            }

            public KeySpec(string id, string label, int vk) : this(id, label, vk, 1.0, 0.0) { }
        }

        public static ObservableCollection<KeyboardKeyViewModel> Create()
        {
            var keys = new ObservableCollection<KeyboardKeyViewModel>();

            AddRow(keys, 0, new KeySpec[] {
                K("ESC","Esc",0x1B), K("F1","F1",0x70,1,.6), K("F2","F2",0x71),
                K("F3","F3",0x72), K("F4","F4",0x73), K("F5","F5",0x74,1,.5),
                K("F6","F6",0x75), K("F7","F7",0x76), K("F8","F8",0x77),
                K("F9","F9",0x78,1,.5), K("F10","F10",0x79), K("F11","F11",0x7A),
                K("F12","F12",0x7B), K("PRTSC","PrtSc",0x2C,1,.4),
                K("FNMINUS","−",0xBD), K("FNPLUS","+",0xBB)
            });

            AddRow(keys, 1, new KeySpec[] {
                K("GRAVE","~",0xC0), K("D1","1",0x31), K("D2","2",0x32), K("D3","3",0x33),
                K("D4","4",0x34), K("D5","5",0x35), K("D6","6",0x36), K("D7","7",0x37),
                K("D8","8",0x38), K("D9","9",0x39), K("D0","0",0x30), K("MINUS","−",0xBD),
                K("EQUAL","=",0xBB), K("BACK","Backspace",0x08,2.0),
                K("INS","Ins",0x2D,1,.35), K("HOME","Home",0x24), K("PGUP","PgUp",0x21),
                K("NUM","Num",0x90), K("NUMDIV","/",0x6F), K("NUMMUL","*",0x6A), K("NUMSUB","−",0x6D)
            });

            AddRow(keys, 2, new KeySpec[] {
                K("TAB","Tab",0x09,1.5), L("Q"), L("W"), L("E"), L("R"), L("T"), L("Y"),
                L("U"), L("I"), L("O"), L("P"), K("LB","[",0xDB), K("RB","]",0xDD),
                K("BSLASH","\\",0xDC,1.5), K("DEL","Del",0x2E,1,.35),
                K("END","End",0x23), K("PGDN","PgDn",0x22),
                K("NUM7","7",0x67), K("NUM8","8",0x68), K("NUM9","9",0x69), K("NUMADD","+",0x6B)
            });

            AddRow(keys, 3, new KeySpec[] {
                K("CAPS","Caps",0x14,1.75), L("A"), L("S"), L("D"), L("F"), L("G"),
                L("H"), L("J"), L("K"), L("L"), K("SEMICOLON",";",0xBA), K("QUOTE","'",0xDE),
                K("ENTER","Enter",0x0D,2.25), K("NUM4","4",0x64,1,3.0),
                K("NUM5","5",0x65), K("NUM6","6",0x66)
            });

            AddRow(keys, 4, new KeySpec[] {
                K("LSHIFT","Shift",0x10,2.25), L("Z"), L("X"), L("C"), L("V"), L("B"),
                L("N"), L("M"), K("COMMA",",",0xBC), K("PERIOD",".",0xBE), K("SLASH","/",0xBF),
                KEx("RSHIFT","Shift",0x10,2.75,0,KeyHeight,0x36,false),
                KEx("UP","↑",0x26,1,3.0,KeyHeight, true), K("NUM1","1",0x61,1,.6),
                K("NUM2","2",0x62), K("NUM3","3",0x63),
                KEx("NUMENTER","Enter",0x0D,1,0,KeyHeight*2+RowGap, true)
            });

            AddRow(keys, 5, new KeySpec[] {
                K("LCTRL","Ctrl",0x11,1.25), KEx("LWIN","Win",0x5B,1.25,0,KeyHeight,true),
                K("LALT","Alt",0x12,1.25), K("SPACE","",0x20,6.25),
                KEx("RALT","Alt",0x12,1.25,0,KeyHeight,true), K("FN","Fn",0x00), KEx("MENU","Menu",0x5D,1.25,0,KeyHeight,true),
                KEx("RCTRL","Ctrl",0x11,1.25,0,KeyHeight,true), KEx("LEFT","←",0x25,1,.6,KeyHeight,true),
                KEx("DOWN","↓",0x28,1,0,KeyHeight,true), KEx("RIGHT","→",0x27,1,0,KeyHeight,true),
                K("NUM0","0",0x60,2,.6), K("NUMDOT",".",0x6E)
            });

            return keys;
        }

        public static string KeyIdFromInput(int virtualKey, int scanCode, bool isExtended)
        {
            if (virtualKey == 0x10) return scanCode == 0x36 ? "RSHIFT" : "LSHIFT";
            if (virtualKey == 0x11) return isExtended ? "RCTRL" : "LCTRL";
            if (virtualKey == 0x12) return isExtended ? "RALT" : "LALT";
            if (virtualKey == 0x0D) return isExtended ? "NUMENTER" : "ENTER";
            if (virtualKey == 0x5B) return "LWIN";
            if (virtualKey == 0x5C) return "RWIN";

            // Navigation virtual keys are shared with NumPad when NumLock is off.
            if (virtualKey == 0x24) return isExtended ? "HOME" : "NUM7";
            if (virtualKey == 0x21) return isExtended ? "PGUP" : "NUM9";
            if (virtualKey == 0x23) return isExtended ? "END" : "NUM1";
            if (virtualKey == 0x22) return isExtended ? "PGDN" : "NUM3";
            if (virtualKey == 0x2D) return isExtended ? "INS" : "NUM0";
            if (virtualKey == 0x2E) return isExtended ? "DEL" : "NUMDOT";
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

        private static KeySpec L(string value)
        {
            return K(value, value, char.ToUpperInvariant(value[0]));
        }

        private static KeySpec K(string id, string label, int vk)
        {
            return new KeySpec(id, label, vk);
        }

        private static KeySpec K(string id, string label, int vk, double width)
        {
            return new KeySpec(id, label, vk, width, 0);
        }

        private static KeySpec K(string id, string label, int vk, double width, double gapBefore)
        {
            return new KeySpec(id, label, vk, width, gapBefore);
        }

        private static KeySpec K(string id, string label, int vk, double width, double gapBefore, int scanCode)
        {
            var spec = new KeySpec(id, label, vk, width, gapBefore);
            spec.ScanCode = scanCode;
            return spec;
        }

        private static KeySpec KEx(
            string id, string label, int vk, double width, double gapBefore,
            double height, bool isExtended)
        {
            return new KeySpec(id, label, vk, width, gapBefore, height, isExtended);
        }

        private static void AddRow(
            ObservableCollection<KeyboardKeyViewModel> target,
            int row,
            KeySpec[] specifications)
        {
            double x = 0;
            double y = row * (KeyHeight + RowGap);
            for (int i = 0; i < specifications.Length; i++)
            {
                KeySpec spec = specifications[i];
                x += spec.GapBefore * (Unit + Gap);
                double width = Unit * spec.Width + Gap * (spec.Width - 1);
                target.Add(new KeyboardKeyViewModel(
                    spec.Id, spec.Label, spec.VirtualKey, spec.ScanCode,
                    x, y, width, spec.Height, spec.IsExtended));
                x += width + Gap;
            }
        }
    }
}
