using System;
using System.IO;
using Serilog;
using Serilog.Events;
using Serilog.Core;
using Serilog.Context;

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
        private static bool _isInitialized = false;

        static AppLogger()
        {
            // Initial fallback logger: logs only to a basic system.log in the execution directory
            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .Enrich.FromLogContext()
                    .Enrich.With(new ThreadIdEnricher())
                    .WriteTo.File(
                        Path.Combine(logDir, "system.log"),
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}",
                        rollingInterval: RollingInterval.Day)
                    .CreateLogger();
            }
            catch
            {
                // Fallback to no-op or Console if even logs dir cannot be created
                Log.Logger = new LoggerConfiguration().CreateLogger();
            }
        }

        public static void Initialize(AppConfig config, string? logDirOverride = null)
        {
            if (_isInitialized && string.IsNullOrEmpty(logDirOverride)) return;

            try
            {
                if (_isInitialized)
                {
                    Log.CloseAndFlush();
                }

                string logDir = logDirOverride ?? string.Empty;
                if (string.IsNullOrEmpty(logDir))
                {
                    if (OperatingSystem.IsWindows())
                    {
                        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        logDir = Path.Combine(appData, "PolyglotCLI", config.LogDirectory);
                    }
                    else
                    {
                        logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, config.LogDirectory);
                    }
                }

                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                // Parse minimum level for file logs
                if (!Enum.TryParse<LogEventLevel>(config.LogLevelFile, true, out var fileLevel))
                {
                    fileLevel = LogEventLevel.Debug;
                }

                var loggerConfig = new LoggerConfiguration()
                    .MinimumLevel.Is(fileLevel)
                    .Enrich.FromLogContext()
                    .Enrich.With(new ThreadIdEnricher());

                // 1. Unified log file sink (polyglot.log) - holds all logs
                string unifiedLogPath = Path.Combine(logDir, "polyglot.log");
                loggerConfig.WriteTo.File(
                    unifiedLogPath,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{ThreadId}] [{ProcessType}] {Message:lj}{NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 31);

                // 2. Process-specific split log files (e.g. logs/extraction.log)
                loggerConfig.WriteTo.Map(
                    "ProcessType",
                    "System",
                    (processType, wt) => {
                        // Sanitize process name for filename compatibility
                        string safeName = string.Join("_", processType.Split(Path.GetInvalidFileNameChars())).ToLowerInvariant();
                        wt.File(
                            Path.Combine(logDir, $"{safeName}.log"),
                            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}",
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 31);
                    });

                Log.Logger = loggerConfig.CreateLogger();
                _isInitialized = true;

                Log.Information("==================================================");
                Log.Information("Logger Re-initialized with AppConfig settings.");
                Log.Information("Log Directory: {LogDir}", logDir);
                Log.Information("File Log Level: {FileLevel}", fileLevel);
                Log.Information("==================================================");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FATAL] Failed to initialize Serilog AppLogger: {ex.Message}");
                Console.ResetColor();
            }
        }

        public static IDisposable BeginProcess(string processName)
        {
            return LogContext.PushProperty("ProcessType", processName);
        }

        public static void Shutdown()
        {
            Log.CloseAndFlush();
        }

        public static void Debug(string message) => Log.Debug(message);
        public static void Info(string message) => Log.Information(message);
        public static void Warn(string message) => Log.Warning(message);
        public static void Error(string message, Exception? ex = null) => Log.Error(ex, message);
        public static void Fatal(string message, Exception? ex = null) => Log.Fatal(ex, message);

        public static void InfoConsole(string message, ConsoleColor? color = null)
        {
            Log.Information(message);
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

        public static void WarnConsole(string message)
        {
            Log.Warning(message);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public static void ErrorConsole(string message, Exception? ex = null)
        {
            Log.Error(ex, message);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            if (ex != null)
            {
                Console.WriteLine($"Error details: {ex.Message}");
            }
            Console.ResetColor();
        }

        private class ThreadIdEnricher : ILogEventEnricher
        {
            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ThreadId", Environment.CurrentManagedThreadId));
            }
        }
    }
}
