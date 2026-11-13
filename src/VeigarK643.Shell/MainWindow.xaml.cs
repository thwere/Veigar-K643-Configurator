using System;
using System.ComponentModel;
using Forms = System.Windows.Forms;
using System.Windows;

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

        StateChanged += (_, _) => WindowStateStore.Save(WindowState);
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
}
