using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using VeigarConfigurator.App.Commands;
using VeigarConfigurator.App.Profiles;
using VeigarConfigurator.Core.Devices;

namespace VeigarConfigurator.App.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IDeviceController _deviceController;
        private readonly DispatcherTimer _deviceTimer;
        private bool _isConnected;
        private bool _isBusy;
        private bool _disposed;
        private string _connectionTitle;
        private string _connectionMessage;
        private string _operationMessage;
        private string _footerStatus;
        private string _profileName;
        private string _hexInput;
        private string _effectiveColorHex;
        private double _redValue;
        private double _greenValue;
        private double _blueValue;
        private double _brightnessValue;
        private Brush _previewBrush;

        public MainViewModel(IDeviceController deviceController)
        {
            _deviceController = deviceController;

            _redValue = 255;
            _greenValue = 32;
            _blueValue = 32;
            _brightnessValue = 100;
            _profileName = "My lighting profile";
            _hexInput = "FF2020";
            _previewBrush = new SolidColorBrush(Color.FromRgb(255, 32, 32));
            _effectiveColorHex = "#FF2020";

            ConnectionTitle = "Searching for Veigar K643...";
            ConnectionMessage = "Checking USB HID interfaces.";
            OperationMessage = "Connect the keyboard to begin.";
            FooterStatus = "Checking device";

            RefreshCommand = new RelayCommand(
                delegate { RefreshDevice(); },
                delegate { return !IsBusy; });

            SendColorCommand = new RelayCommand(
                delegate(object parameter) { SendColor(parameter); },
                delegate { return IsConnected && !IsBusy; });

            ApplyHexCommand = new RelayCommand(
                delegate { ApplyHexColor(); },
                delegate { return IsConnected && !IsBusy; });

            SaveProfileCommand = new RelayCommand(
                delegate { SaveProfile(); },
                delegate { return !IsBusy; });

            LoadProfileCommand = new RelayCommand(
                delegate { LoadProfile(); },
                delegate { return !IsBusy; });

            _deviceTimer = new DispatcherTimer();
            _deviceTimer.Interval = TimeSpan.FromSeconds(2);
            _deviceTimer.Tick += DeviceTimer_Tick;
            _deviceTimer.Start();

            RefreshDevice();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand RefreshCommand { get; private set; }
        public ICommand SendColorCommand { get; private set; }
        public ICommand ApplyHexCommand { get; private set; }
        public ICommand SaveProfileCommand { get; private set; }
        public ICommand LoadProfileCommand { get; private set; }

        public bool IsConnected
        {
            get { return _isConnected; }
            private set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged("IsConnected");
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            private set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    OnPropertyChanged("IsBusy");
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ConnectionTitle
        {
            get { return _connectionTitle; }
            private set { SetField(ref _connectionTitle, value, "ConnectionTitle"); }
        }

        public string ConnectionMessage
        {
            get { return _connectionMessage; }
            private set { SetField(ref _connectionMessage, value, "ConnectionMessage"); }
        }

        public string OperationMessage
        {
            get { return _operationMessage; }
            private set { SetField(ref _operationMessage, value, "OperationMessage"); }
        }

        public string FooterStatus
        {
            get { return _footerStatus; }
            private set { SetField(ref _footerStatus, value, "FooterStatus"); }
        }

        public string ProfileName
        {
            get { return _profileName; }
            set { SetField(ref _profileName, value, "ProfileName"); }
        }

        public string HexInput
        {
            get { return _hexInput; }
            set { SetField(ref _hexInput, value, "HexInput"); }
        }

        public string EffectiveColorHex
        {
            get { return _effectiveColorHex; }
            private set { SetField(ref _effectiveColorHex, value, "EffectiveColorHex"); }
        }

        public Brush PreviewBrush
        {
            get { return _previewBrush; }
            private set { SetField(ref _previewBrush, value, "PreviewBrush"); }
        }

        public double RedValue
        {
            get { return _redValue; }
            set
            {
                double next = Clamp(value, 0, 255);
                if (Math.Abs(_redValue - next) > 0.001)
                {
                    _redValue = next;
                    OnPropertyChanged("RedValue");
                    UpdateColorPresentation(true);
                }
            }
        }

        public double GreenValue
        {
            get { return _greenValue; }
            set
            {
                double next = Clamp(value, 0, 255);
                if (Math.Abs(_greenValue - next) > 0.001)
                {
                    _greenValue = next;
                    OnPropertyChanged("GreenValue");
                    UpdateColorPresentation(true);
                }
            }
        }

        public double BlueValue
        {
            get { return _blueValue; }
            set
            {
                double next = Clamp(value, 0, 255);
                if (Math.Abs(_blueValue - next) > 0.001)
                {
                    _blueValue = next;
                    OnPropertyChanged("BlueValue");
                    UpdateColorPresentation(true);
                }
            }
        }

        public double BrightnessValue
        {
            get { return _brightnessValue; }
            set
            {
                double next = Clamp(value, 0, 100);
                if (Math.Abs(_brightnessValue - next) > 0.001)
                {
                    _brightnessValue = next;
                    OnPropertyChanged("BrightnessValue");
                    UpdateColorPresentation(false);
                }
            }
        }

        private void DeviceTimer_Tick(object sender, EventArgs e)
        {
            if (!IsBusy)
            {
                RefreshDevice();
            }
        }

        private void RefreshDevice()
        {
            try
            {
                DeviceConnectionInfo info = _deviceController.RefreshConnection();
                IsConnected = info.IsConnected;
                ConnectionTitle = info.Title;
                ConnectionMessage = info.Message;
                FooterStatus = info.IsConnected ? "Device ready" : "Device unavailable";

                if (!info.IsConnected)
                {
                    OperationMessage = "RGB controls are unavailable until the target HID interface is connected.";
                }
            }
            catch (Exception exception)
            {
                IsConnected = false;
                ConnectionTitle = "Device check failed";
                ConnectionMessage = exception.Message;
                FooterStatus = "Device check error";
            }
        }

        private async void SendColor(object parameter)
        {
            if (!IsConnected || IsBusy)
            {
                return;
            }

            string preset = parameter as string;
            if (!string.IsNullOrWhiteSpace(preset))
            {
                byte presetRed;
                byte presetGreen;
                byte presetBlue;
                if (!TryParseColor(preset, out presetRed, out presetGreen, out presetBlue))
                {
                    OperationMessage = "Invalid preset colour.";
                    return;
                }

                RedValue = presetRed;
                GreenValue = presetGreen;
                BlueValue = presetBlue;
            }

            byte red = ScaleChannel(RedValue, BrightnessValue);
            byte green = ScaleChannel(GreenValue, BrightnessValue);
            byte blue = ScaleChannel(BlueValue, BrightnessValue);

            IsBusy = true;
            OperationMessage = "Sending one RGB Feature Report...";
            FooterStatus = "Updating keyboard";

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                await _deviceController.SendSolidColorPreviewAsync(
                    red, green, blue, CancellationToken.None);

                stopwatch.Stop();
                OperationMessage = string.Format(
                    "Applied {0} in {1} ms. One 382-byte HID report was sent.",
                    EffectiveColorHex,
                    stopwatch.ElapsedMilliseconds);
                FooterStatus = "Lighting updated";
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                OperationMessage = "RGB update failed: " + exception.Message;
                FooterStatus = "RGB update error";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyHexColor()
        {
            byte red;
            byte green;
            byte blue;
            if (!TryParseColor(HexInput, out red, out green, out blue))
            {
                OperationMessage = "Enter a six-digit HEX colour, for example FF2020.";
                return;
            }

            RedValue = red;
            GreenValue = green;
            BlueValue = blue;
            SendColor(null);
        }

        private void SaveProfile()
        {
            var dialog = new SaveFileDialog();
            dialog.Title = "Save Veigar lighting profile";
            dialog.Filter = "Veigar profile (*.json)|*.json";
            dialog.DefaultExt = ".json";
            dialog.AddExtension = true;
            dialog.FileName = MakeSafeFileName(ProfileName) + ".json";

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                var profile = new LightingProfile
                {
                    Name = string.IsNullOrWhiteSpace(ProfileName) ? "My lighting profile" : ProfileName.Trim(),
                    Red = ToByte(RedValue),
                    Green = ToByte(GreenValue),
                    Blue = ToByte(BlueValue),
                    Brightness = (int)Math.Round(BrightnessValue)
                };

                LightingProfileStore.Save(dialog.FileName, profile);
                OperationMessage = "Profile saved locally: " + dialog.FileName;
                FooterStatus = "Profile saved";
            }
            catch (Exception exception)
            {
                OperationMessage = "Could not save profile: " + exception.Message;
                FooterStatus = "Profile save error";
            }
        }

        private void LoadProfile()
        {
            var dialog = new OpenFileDialog();
            dialog.Title = "Load Veigar lighting profile";
            dialog.Filter = "Veigar profile (*.json)|*.json|All files (*.*)|*.*";
            dialog.CheckFileExists = true;
            dialog.Multiselect = false;

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                LightingProfile profile = LightingProfileStore.Load(dialog.FileName);
                ProfileName = profile.Name ?? "Loaded profile";
                RedValue = Clamp(profile.Red, 0, 255);
                GreenValue = Clamp(profile.Green, 0, 255);
                BlueValue = Clamp(profile.Blue, 0, 255);
                BrightnessValue = Clamp(profile.Brightness, 0, 100);
                OperationMessage = "Profile loaded into the editor. Press APPLY to send it to the keyboard.";
                FooterStatus = "Profile loaded";
            }
            catch (Exception exception)
            {
                OperationMessage = "Could not load profile: " + exception.Message;
                FooterStatus = "Profile load error";
            }
        }

        private void UpdateColorPresentation(bool updateHexInput)
        {
            byte rawRed = ToByte(RedValue);
            byte rawGreen = ToByte(GreenValue);
            byte rawBlue = ToByte(BlueValue);

            if (updateHexInput)
            {
                HexInput = string.Format("{0:X2}{1:X2}{2:X2}", rawRed, rawGreen, rawBlue);
            }

            byte red = ScaleChannel(rawRed, BrightnessValue);
            byte green = ScaleChannel(rawGreen, BrightnessValue);
            byte blue = ScaleChannel(rawBlue, BrightnessValue);

            PreviewBrush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            EffectiveColorHex = string.Format("#{0:X2}{1:X2}{2:X2}", red, green, blue);
        }

        private static byte ScaleChannel(double channel, double brightness)
        {
            return (byte)Math.Round(Clamp(channel, 0, 255) * Clamp(brightness, 0, 100) / 100.0);
        }

        private static byte ToByte(double value)
        {
            return (byte)Math.Round(Clamp(value, 0, 255));
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static bool TryParseColor(
            string value,
            out byte red,
            out byte green,
            out byte blue)
        {
            red = 0;
            green = 0;
            blue = 0;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim().TrimStart('#');
            if (normalized.Length != 6)
            {
                return false;
            }

            try
            {
                red = Convert.ToByte(normalized.Substring(0, 2), 16);
                green = Convert.ToByte(normalized.Substring(2, 2), 16);
                blue = Convert.ToByte(normalized.Substring(4, 2), 16);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string MakeSafeFileName(string value)
        {
            string name = string.IsNullOrWhiteSpace(value) ? "Veigar-profile" : value.Trim();
            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '-');
            }

            return name;
        }

        private void SetField<T>(ref T field, T value, string propertyName)
        {
            if (!object.Equals(field, value))
            {
                field = value;
                OnPropertyChanged(propertyName);
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _deviceTimer.Stop();
            _deviceTimer.Tick -= DeviceTimer_Tick;
            _deviceController.Dispose();
        }
    }
}
