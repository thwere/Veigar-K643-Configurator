using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace VeigarConfigurator.App.Models
{
    [DataContract]
    public sealed class LightingLayer : INotifyPropertyChanged
    {
        private bool _isEnabled;
        private string _name;
        private string _mode;
        private string _blendMode;
        private int _opacity;
        private RgbColor _startColor;
        private RgbColor _endColor;
        private string _keyEffect;

        public LightingLayer()
        {
            _isEnabled = true;
            _opacity = 100;
            _blendMode = "Replace";
            _startColor = new RgbColor(255, 32, 32);
            _endColor = new RgbColor(32, 80, 255);
            SlotColors = new List<LedSlotColor>();
            Frames = new List<AnimationFrame>();
            _keyEffect = "Fade";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [DataMember(Order = 1)]
        public string Name
        {
            get { return _name; }
            set { SetField(ref _name, value, "Name"); }
        }

        [DataMember(Order = 2)]
        public string Mode
        {
            get { return _mode; }
            set { SetField(ref _mode, value, "Mode"); }
        }

        [DataMember(Order = 3)]
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { SetField(ref _isEnabled, value, "IsEnabled"); }
        }

        [DataMember(Order = 4)]
        public string BlendMode
        {
            get { return _blendMode; }
            set { SetField(ref _blendMode, value, "BlendMode"); }
        }

        [DataMember(Order = 5)]
        public int Opacity
        {
            get { return _opacity; }
            set { SetField(ref _opacity, value, "Opacity"); }
        }

        [DataMember(Order = 6)]
        public RgbColor StartColor
        {
            get { return _startColor; }
            set { SetField(ref _startColor, value, "StartColor"); }
        }

        [DataMember(Order = 7)]
        public RgbColor EndColor
        {
            get { return _endColor; }
            set { SetField(ref _endColor, value, "EndColor"); }
        }

        [DataMember(Order = 8)]
        public List<LedSlotColor> SlotColors { get; set; }

        [DataMember(Order = 9)]
        public List<AnimationFrame> Frames { get; set; }

        [DataMember(Order = 10)]
        public string KeyEffect
        {
            get { return _keyEffect; }
            set { SetField(ref _keyEffect, value, "KeyEffect"); }
        }

        private void SetField<T>(ref T field, T value, string propertyName)
        {
            if (object.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
