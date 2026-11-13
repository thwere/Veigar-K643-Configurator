using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace VeigarK643.Shell;

public partial class App : Application
{
    private const string MutexName = @"Local\VeigarK643Configurator.Singleton";
    private const string ActivationEventName = @"Local\VeigarK643Configurator.Activate";

    private Mutex? _instanceMutex;
    private EventWaitHandle? _activationSignal;
    private Thread? _activationThread;
    private bool _ownsMutex;
    private volatile bool _isShuttingDown;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Create the activation event before acquiring the mutex so a second
        // process can always signal the first process during startup.
        _activationSignal = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: ActivationEventName);

        _instanceMutex = new Mutex(
            initiallyOwned: true,
            name: MutexName,
            createdNew: out _ownsMutex);

        if (!_ownsMutex)
        {
            try
            {
                _activationSignal.Set();
            }
            finally
            {
                _activationSignal.Dispose();
                _instanceMutex.Dispose();
            }

            Shutdown();
            return;
        }

        base.OnStartup(e);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();

        StartActivationListener();
    }

    private void StartActivationListener()
    {
        _activationThread = new Thread(() =>
        {
            while (!_isShuttingDown)
            {
                _activationSignal!.WaitOne();

                if (_isShuttingDown)
                {
                    return;
                }

                try
                {
                    Dispatcher.BeginInvoke(
                        DispatcherPriority.Normal,
                        new Action(() =>
                        {
                            if (MainWindow is MainWindow window)
                            {
                                window.RestoreFromTray();
                            }
                        }));
                }
                catch (InvalidOperationException)
                {
                    // The dispatcher is already shutting down.
                    return;
                }
            }
        })
        {
            IsBackground = true,
            Name = "Single-instance activation listener"
        };

        _activationThread.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _isShuttingDown = true;

        try
        {
            _activationSignal?.Set();
            _activationThread?.Join(millisecondsTimeout: 500);
        }
        catch (ObjectDisposedException)
        {
            // The signal may already have been disposed during startup.
        }

        _activationSignal?.Dispose();

        if (_ownsMutex && _instanceMutex is not null)
        {
            try
            {
                _instanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex is no longer owned by this process.
            }
        }

        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
