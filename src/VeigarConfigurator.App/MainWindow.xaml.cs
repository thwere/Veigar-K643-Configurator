using System;
using System.Windows;
using VeigarConfigurator.App.ViewModels;
using VeigarConfigurator.Devices.Redragon;
using VeigarConfigurator.Windows.Hid;

namespace VeigarConfigurator.App
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            var enumerator = new HidDeviceEnumerator();
            var transport = new HidFeatureTransport();
            var controller = new VeigarK643Controller(enumerator, transport);

            _viewModel = new MainViewModel(controller);
            DataContext = _viewModel;

            Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _viewModel.Dispose();
        }
    }
}
