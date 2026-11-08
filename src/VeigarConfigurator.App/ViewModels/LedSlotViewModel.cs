using System;
using System.ComponentModel;
using System.Windows.Media;

namespace VeigarConfigurator.App.ViewModels
{
    public sealed class LedSlotViewModel : INotifyPropertyChanged
    {
        private Brush _brush;
        private bool _isSelected;
        private string _mappingName;

        public LedSlotViewModel(int slotIndex)
        {
            SlotIndex = slotIndex;
            _brush = new SolidColorBrush(Color.FromRgb(32, 34, 38));
            _mappingName = string.Empty;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public int SlotIndex { get; private set; }

        public string Label
        {
            get { return (SlotIndex + 1).ToString("000"); }
        }

        public Brush Brush
        {
            get { return _brush; }
            set { SetField(ref _brush, value, "Brush"); }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetField(ref _isSelected, value, "IsSelected"); }
        }

        public string MappingName
        {
            get { return _mappingName; }
            set { SetField(ref _mappingName, value, "MappingName"); }
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
