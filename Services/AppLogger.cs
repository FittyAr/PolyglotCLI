using System;
using System.IO;
using System.Threading;

namespace PolyglotCLI
{
    public enum LogLevel
    {
        DEBUG,
        INFO,
        WARN,
        ERROR,
        FATAL
    }

    public static class AppLogger
    {
        private static readonly object _lock = new object();
        private static string _logFilePath = string.Empty;

        static AppLogger()
        {
            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
                _logFilePath = Path.Combine(logDir, "polyglot.log");

                // If log file exists and is larger than 10MB, rotate it
                if (File.Exists(_logFilePath))
                {
                    var fileInfo = new FileInfo(_logFilePath);
                    if (fileInfo.Length > 10 * 1024 * 1024) 
                    {
                        string backupPath = Path.Combine(logDir, $"polyglot_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                        File.Move(_logFilePath, backupPath);
                    }
                }

                Info("==================================================");
                Info($"Logger Initialized. Log file: {_logFilePath}");
                Info("==================================================");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FATAL] Failed to initialize AppLogger: {ex.Message}");
                Console.ResetColor();
            }
        }

        private static void WriteLog(LogLevel level, string message, Exception? exception = null)
        {
            if (string.IsNullOrEmpty(_logFilePath)) return;

            string formattedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{Thread.CurrentThread.ManagedThreadId:D2}] {message}";
            if (exception != null)
            {
                formattedMessage += $"\nException: {exception.GetType().FullName}: {exception.Message}\nStack Trace:\n{exception.StackTrace}";
            }

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_logFilePath, formattedMessage + Environment.NewLine);
                }
                catch
                {
                    // Ignore write failures to prevent crash loops
                }
            }
        }

        public static void Debug(string message) => WriteLog(LogLevel.DEBUG, message);
        public static void Info(string message) => WriteLog(LogLevel.INFO, message);
        public static void Warn(string message) => WriteLog(LogLevel.WARN, message);
        public static void Error(string message, Exception? ex = null) => WriteLog(LogLevel.ERROR, message, ex);
        public static void Fatal(string message, Exception? ex = null) => WriteLog(LogLevel.FATAL, message, ex);

        /// <summary>
        /// Logs the message to the log file and prints it to the Console with optional colors.
        /// </summary>
        public static void InfoConsole(string message, ConsoleColor? color = null)
        {
            Info(message);
            if (color.HasValue)
            {
                Console.ForegroundColor = color.Value;
            }
            Console.WriteLine(message);
            if (color.HasValue)
            {
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Logs the warning message to the log file and prints it to the Console in Yellow.
        /// </summary>
        public static void WarnConsole(string message)
        {
            Warn(message);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        /// <summary>
        /// Logs the error message and exception details to the log file and prints it to the Console in Red.
        /// </summary>
        public static void ErrorConsole(string message, Exception? ex = null)
        {
            Error(message, ex);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            if (ex != null)
            {
                Console.WriteLine($"Error details: {ex.Message}");
            }
            Console.ResetColor();
        }
    }
}
