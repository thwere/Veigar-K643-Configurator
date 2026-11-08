using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using VeigarConfigurator.App.Commands;
using VeigarConfigurator.Core.Devices;

namespace VeigarConfigurator.App.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IDeviceController _deviceController;
        private readonly DispatcherTimer _deviceTimer;
        private bool _isConnected;
        private bool _isBusy;
        private string _connectionTitle;
        private string _connectionMessage;
        private string _operationMessage;
        private string _footerStatus;
        private bool _disposed;

        public MainViewModel(IDeviceController deviceController)
        {
            _deviceController = deviceController;
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

            _deviceTimer = new DispatcherTimer();
            _deviceTimer.Interval = TimeSpan.FromSeconds(2);
            _deviceTimer.Tick += DeviceTimer_Tick;
            _deviceTimer.Start();

            RefreshDevice();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand RefreshCommand { get; private set; }
        public ICommand SendColorCommand { get; private set; }

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
                    OperationMessage = "RGB test is unavailable until the target HID interface is connected.";
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

            byte red;
            byte green;
            byte blue;
            if (!TryParseColor(parameter as string, out red, out green, out blue))
            {
                OperationMessage = "Invalid color parameter.";
                return;
            }

            IsBusy = true;
            OperationMessage = "Sending RGB frames...";
            FooterStatus = "RGB preview running";

            try
            {
                await _deviceController.SendSolidColorPreviewAsync(
                    red, green, blue, CancellationToken.None);

                OperationMessage = "RGB preview completed. The keyboard accepted 60 Feature Report writes.";
                FooterStatus = "RGB preview completed";
            }
            catch (Exception exception)
            {
                OperationMessage = "RGB preview failed: " + exception.Message;
                FooterStatus = "RGB preview error";
            }
            finally
            {
                IsBusy = false;
                RefreshDevice();
            }
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

            if (string.IsNullOrWhiteSpace(value) || value.Length != 6)
            {
                return false;
            }

            try
            {
                red = Convert.ToByte(value.Substring(0, 2), 16);
                green = Convert.ToByte(value.Substring(2, 2), 16);
                blue = Convert.ToByte(value.Substring(4, 2), 16);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private void SetField(ref string field, string value, string propertyName)
        {
            if (field != value)
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
