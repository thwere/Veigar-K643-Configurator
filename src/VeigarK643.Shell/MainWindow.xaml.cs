using System;
using System.ComponentModel;
using Forms = System.Windows.Forms;
using System.Windows;
using System.Windows.Media;
using VeigarK643.Shell.Interop;

namespace VeigarK643.Shell;

public partial class MainWindow : Window
{
    private readonly Forms.NotifyIcon _trayIcon;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "Veigar K643 Configurator",
            Visible = false
        };

        var trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Items.Add("Открыть", null, (_, _) => RestoreFromTray());
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add("Выход", null, (_, _) => ExitApplication());
        _trayIcon.ContextMenuStrip = trayMenu;

        _trayIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                RestoreFromTray();
            }
        };

        if (WindowStateStore.ShouldStartMaximized())
        {
            WindowState = WindowState.Maximized;
        }

        StateChanged += (_, _) =>
        {
            WindowStateStore.Save(WindowState);
            UpdateMaximizeGlyph();
            NativeWindowEffects.UpdateRoundedRegion(this);
        };

        SizeChanged += (_, _) => NativeWindowEffects.UpdateRoundedRegion(this);
        UpdateMaximizeGlyph();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeWindowEffects.Apply(this);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);

        if (!e.Cancel && !_allowClose)
        {
            e.Cancel = true;
            HideToTray();
        }

        if (_allowClose && !e.Cancel)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
    }

    private void RootClipHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = RootClipHost.ActualWidth;
        var height = RootClipHost.ActualHeight;

        if (width > 0 && height > 0)
        {
            RootClipHost.Clip = new RectangleGeometry(
                new Rect(0, 0, width, height),
                radiusX: 12,
                radiusY: 12);
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        HideToTray();
    }

    private void HideToTray()
    {
        WindowStateStore.Save(WindowState);
        _trayIcon.Visible = true;
        Hide();
    }

    public void RestoreFromTray()
    {
        _trayIcon.Visible = false;

        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    private void ExitApplication()
    {
        _allowClose = true;
        _trayIcon.Visible = false;
        Close();
        Application.Current.Shutdown();
    }

    private void UpdateMaximizeGlyph()
    {
        if (MaximizeGlyph is null)
        {
            return;
        }

        MaximizeGlyph.Data = Geometry.Parse(
            WindowState == WindowState.Maximized
                ? "M 7,3 H 17 V 13 H 7 Z M 4,6 H 3 V 17 H 14 V 16"
                : "M 4,4 H 16 V 16 H 4 Z");

        MaximizeButton.ToolTip = WindowState == WindowState.Maximized
            ? "Восстановить"
            : "Развернуть";
    }
}
