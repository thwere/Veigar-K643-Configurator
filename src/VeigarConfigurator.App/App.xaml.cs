using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace VeigarConfigurator.App
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            try
            {
                MainWindow window = new MainWindow();
                MainWindow = window;
                window.Show();
            }
            catch (Exception exception)
            {
                ShowStartupFailure(exception);
                Shutdown(-1);
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowStartupFailure(e.Exception);
            e.Handled = true;
            Shutdown(-1);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception exception = e.ExceptionObject as Exception;
            if (exception != null)
                WriteErrorLog(exception);
        }

        private static void ShowStartupFailure(Exception exception)
        {
            string logPath = WriteErrorLog(exception);
            string message = "Veigar K643 Configurator не удалось запустить или продолжить работу."
                + Environment.NewLine + Environment.NewLine
                + "Ошибка: " + exception.GetBaseException().Message
                + Environment.NewLine + Environment.NewLine
                + (string.IsNullOrEmpty(logPath)
                    ? "Не удалось создать файл журнала."
                    : "Подробный журнал ошибки: " + logPath)
                + Environment.NewLine + Environment.NewLine
                + "Перед повторным запуском закройте программу настройки Redragon.";
            try
            {
                MessageBox.Show(message, "Veigar Configurator — ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // No recovery path if the WPF dispatcher is unavailable.
            }
        }

        private static string WriteErrorLog(Exception exception)
        {
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VeigarConfigurator", "Logs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory,
                    "startup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");

                var report = new StringBuilder();
                report.AppendLine("Veigar K643 Configurator error report");
                report.AppendLine("Local time: " + DateTime.Now.ToString("O"));
                report.AppendLine("OS: " + Environment.OSVersion);
                report.AppendLine("64-bit OS: " + Environment.Is64BitOperatingSystem);
                report.AppendLine("64-bit process: " + Environment.Is64BitProcess);
                report.AppendLine(".NET CLR: " + Environment.Version);
                report.AppendLine("Application directory: " + AppDomain.CurrentDomain.BaseDirectory);
                report.AppendLine();
                report.AppendLine(exception.ToString());
                File.WriteAllText(path, report.ToString(), Encoding.UTF8);
                return path;
            }
            catch
            {
                return null;
            }
        }
    }
}
