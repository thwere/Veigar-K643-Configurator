using System;
using System.Windows;
using System.Windows.Interop;
using VeigarConfigurator.App.UI;
using VeigarConfigurator.App.ViewModels;
using VeigarConfigurator.Devices.Redragon;
using VeigarConfigurator.Windows.Audio;
using VeigarConfigurator.Windows.Hid;
using VeigarConfigurator.Windows.RawInput;

namespace VeigarConfigurator.App
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly RawInputKeyboardListener _rawInputListener;

        public MainWindow()
        {
            InitializeComponent();
            DarkTitleBar.Apply(this);

            var enumerator = new HidDeviceEnumerator();
            var transport = new HidFeatureTransport();
            var controller = new VeigarK643Controller(enumerator, transport);

            AudioPeakMeter audioMeter = null;
            try
            {
                audioMeter = new AudioPeakMeter();
            }
            catch
            {
                // Audio-reactive lighting remains optional when no endpoint meter is available.
            }

            _viewModel = new MainViewModel(controller, audioMeter);
            DataContext = _viewModel;

            _rawInputListener = new RawInputKeyboardListener();
            _rawInputListener.KeyReceived += RawInputListener_KeyReceived;
            SourceInitialized += MainWindow_SourceInitialized;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            bool registered = _rawInputListener.Register(handle);

            if (!registered)
            {
                _viewModel.OnRawInputStatus(
                    "Raw Input registration failed. Key mapping and key-reactive lighting are unavailable.");
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _rawInputListener.KeyReceived -= RawInputListener_KeyReceived;
            _rawInputListener.Dispose();
            _viewModel.Dispose();
        }

        private void RawInputListener_KeyReceived(object sender, RawKeyEventArgs e)
        {
            _viewModel.OnPhysicalKeyReceived(e);
        }
    }
}
