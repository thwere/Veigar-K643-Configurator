using System;
using System.ComponentModel;
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
            try { audioMeter = new AudioPeakMeter(); }
            catch { /* Audio-reactive lighting remains optional. */ }

            _viewModel = new MainViewModel(controller, audioMeter);
            DataContext = _viewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            ApplyTheme();

            _rawInputListener = new RawInputKeyboardListener();
            _rawInputListener.KeyReceived += RawInputListener_KeyReceived;
            SourceInitialized += MainWindow_SourceInitialized;
            Closed += MainWindow_Closed;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsLightTheme")
                ApplyTheme();
        }

        private void ApplyTheme()
        {
            bool light = _viewModel != null && _viewModel.IsLightTheme;
            Resources["WindowBackgroundBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromRgb(242, 246, 250) : ColorFromRgb(12, 18, 26));
            Resources["PanelBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromArgb(238, 255, 255, 255) : ColorFromArgb(217, 26, 38, 51));
            Resources["PanelSecondaryBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromArgb(228, 224, 233, 241) : ColorFromArgb(181, 31, 44, 58));
            Resources["LineBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromRgb(185, 198, 211) : ColorFromRgb(70, 81, 95));
            Resources["TextBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromRgb(29, 42, 56) : ColorFromRgb(233, 238, 245));
            Resources["MutedBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromRgb(99, 115, 132) : ColorFromRgb(143, 156, 171));
            Resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromRgb(196, 68, 83) : ColorFromRgb(228, 90, 99));
            Resources["AccentSoftBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromRgb(249, 225, 229) : ColorFromRgb(89, 48, 57));
            Resources["KeyBaseBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromRgb(216, 225, 234) : ColorFromRgb(48, 58, 71));
            Resources["KeyTextBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromRgb(50, 64, 79) : ColorFromRgb(230, 236, 243));
            Resources["KeyTopEdgeBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromArgb(210, 255, 255, 255) : ColorFromArgb(110, 222, 232, 244));
            Resources["KeyboardCanvasBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromRgb(222, 230, 238) : ColorFromRgb(24, 34, 46));
            Resources["ScrollTrackBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromRgb(218, 227, 236) : ColorFromRgb(25, 37, 50));
            Resources["ScrollThumbBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromRgb(143, 158, 174) : ColorFromRgb(88, 105, 124));
            Resources["SliderTrackBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromRgb(188, 202, 216) : ColorFromRgb(64, 80, 99));
            Resources["SliderThumbBrush"] = new System.Windows.Media.SolidColorBrush(
                light ? ColorFromRgb(196, 68, 83) : ColorFromRgb(228, 90, 99));
            DarkTitleBar.Set(this, !light);
        }

        private static System.Windows.Media.Color ColorFromRgb(byte r, byte g, byte b)
        {
            return System.Windows.Media.Color.FromRgb(r, g, b);
        }

        private static System.Windows.Media.Color ColorFromArgb(byte a, byte r, byte g, byte b)
        {
            return System.Windows.Media.Color.FromArgb(a, r, g, b);
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                bool registered = _rawInputListener.Register(handle);
                if (!registered)
                    _viewModel.OnRawInputStatus("Raw Input registration failed. Key mapping and physical painting are unavailable.");
            }
            catch (Exception exception)
            {
                _viewModel.OnRawInputStatus("Raw Input is unavailable: " + exception.Message);
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
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
