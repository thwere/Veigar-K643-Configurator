using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace VeigarConfigurator.Windows.Diagnostics
{
    /// <summary>
    /// Small, filtered diagnostics console for the desktop application.
    /// Default output contains lifecycle, warning/error and target-keyboard events.
    /// Set VEIGAR_LOG_LEVEL=TRACE to include verbose diagnostics.
    /// </summary>
    public static class DiagnosticLog
    {
        private const uint AttachParentProcess = 0xFFFFFFFF;
        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private static bool _traceEnabled;
        private static StreamWriter _consoleWriter;
        private static StreamWriter _fileWriter;

        public static string LogFilePath { get; private set; }

        public static void Initialize()
        {
            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                _initialized = true;
                _traceEnabled = string.Equals(
                    Environment.GetEnvironmentVariable("VEIGAR_LOG_LEVEL"),
                    "TRACE",
                    StringComparison.OrdinalIgnoreCase);

                bool consoleReady = GetConsoleWindow() != IntPtr.Zero;
                if (!consoleReady)
                {
                    consoleReady = AttachConsole(AttachParentProcess) || AllocConsole();
                }

                if (consoleReady)
                {
                    try
                    {
                        Console.OutputEncoding = new UTF8Encoding(false);
                        Console.Title = "Veigar K643 Configurator - Diagnostics";
                        _consoleWriter = new StreamWriter(
                            Console.OpenStandardOutput(),
                            new UTF8Encoding(false));
                        _consoleWriter.AutoFlush = true;
                        Console.SetOut(_consoleWriter);
                        Console.SetError(_consoleWriter);
                    }
                    catch
                    {
                        _consoleWriter = null;
                    }
                }

                try
                {
                    string directory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "VeigarConfigurator",
                        "Logs");
                    Directory.CreateDirectory(directory);

                    LogFilePath = Path.Combine(
                        directory,
                        "session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");

                    _fileWriter = new StreamWriter(
                        new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                        new UTF8Encoding(false));
                    _fileWriter.AutoFlush = true;
                }
                catch
                {
                    _fileWriter = null;
                }

                WriteLocked("INFO", "DIAGNOSTICS", "Diagnostic logging initialized.");
                WriteLocked("INFO", "DIAGNOSTICS",
                    "Filter: INFO, WARN, ERROR and target keyboard events. TRACE is disabled by default.");
                if (_traceEnabled)
                {
                    WriteLocked("INFO", "DIAGNOSTICS",
                        "Verbose TRACE logging is enabled by VEIGAR_LOG_LEVEL=TRACE.");
                }

                if (!string.IsNullOrEmpty(LogFilePath))
                {
                    WriteLocked("INFO", "DIAGNOSTICS", "Log file: " + LogFilePath);
                }

                if (!consoleReady)
                {
                    Debug.WriteLine("Veigar diagnostics console could not be opened; file logging remains active.");
                }
            }
        }

        public static void Info(string category, string message)
        {
            Write("INFO", category, message);
        }

        public static void Input(string message)
        {
            Write("INPUT", "RAWINPUT", message);
        }

        public static void Warning(string category, string message)
        {
            Write("WARN", category, message);
        }

        public static void Error(string category, string message, Exception exception)
        {
            string details = exception == null
                ? message
                : message + Environment.NewLine + exception;
            Write("ERROR", category, details);
        }

        public static void Trace(string category, string message)
        {
            if (_traceEnabled)
            {
                Write("TRACE", category, message);
            }
        }

        public static void Shutdown()
        {
            lock (SyncRoot)
            {
                if (_consoleWriter != null)
                {
                    try { _consoleWriter.Flush(); }
                    catch { }
                }

                if (_fileWriter != null)
                {
                    try
                    {
                        _fileWriter.Flush();
                        _fileWriter.Dispose();
                    }
                    catch { }
                    _fileWriter = null;
                }
            }
        }

        private static void Write(string level, string category, string message)
        {
            lock (SyncRoot)
            {
                WriteLocked(level, category, message);
            }
        }

        private static void WriteLocked(string level, string category, string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")
                + " [" + level + "]"
                + " [" + (category ?? "GENERAL") + "] "
                + (message ?? string.Empty);

            Debug.WriteLine(line);

            if (_consoleWriter != null)
            {
                try { _consoleWriter.WriteLine(line); }
                catch { }
            }

            if (_fileWriter != null)
            {
                try { _fileWriter.WriteLine(line); }
                catch { }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();
    }
}
