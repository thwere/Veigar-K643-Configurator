using System.Windows;

namespace VeigarK643.Shell;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (WindowStateStore.ShouldStartMaximized())
        {
            WindowState = WindowState.Maximized;
        }

        StateChanged += (_, _) => WindowStateStore.Save(WindowState);
    }

    public void ActivateWindow()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }
}
