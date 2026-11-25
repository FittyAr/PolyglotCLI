using System;
using System.IO;
using Serilog;
using Serilog.Events;
using Serilog.Core;
using Serilog.Context;
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
        private static bool _isInitialized = false;

        /// <summary>
        /// Logger adicional del job en curso (archivo en {job}\logs\).
        /// Es AsyncLocal: sólo recibe los mensajes del flujo asíncrono de
        /// ese job, así dos ejecuciones concurrentes no mezclan logs y el
        /// logger global nunca se re-apunta a la carpeta de un job.
        /// </summary>
        private static readonly AsyncLocal<ILogger?> _jobLogger = new();

        private const string LogOutputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{ThreadId}] [{ProcessType}] {Message:lj}{NewLine}{Exception}";
        private const string ProcessLogOutputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{ThreadId}] {Message:lj}{NewLine}{Exception}";
        private const string DefaultLogDirectoryName = "logs";
        private const int RetainedLogFiles = 31;

        // Event for UI binding (Blazor streaming)
        public static event Action<string, LogLevel>? OnLogMessage;

        static AppLogger()
        {
            // Logger inicial (antes de Initialize): misma carpeta que usa
            // Initialize. NUNCA BaseDirectory\logs: es de sólo lectura en
            // MSIX / Program Files.
            try
            {
                string logDir = GetDefaultLogDirectory(DefaultLogDirectoryName);
                Directory.CreateDirectory(logDir);

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .Enrich.FromLogContext()
                    .Enrich.With(new ThreadIdEnricher())
                    .WriteTo.File(
                        Path.Combine(logDir, "system.log"),
                        outputTemplate: ProcessLogOutputTemplate,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: RetainedLogFiles)
                    .CreateLogger();
            }
            catch
            {
                // Fallback to no-op if even logs dir cannot be created
                Log.Logger = new LoggerConfiguration().CreateLogger();
            }
        }

        /// <summary>
        /// Carpeta de logs global: %AppData%\FittyAr\PolyglotCLI\{nombre}
        /// en Windows; junto al binario en otras plataformas.
        /// </summary>
        public static string GetDefaultLogDirectory(string? logDirectoryName = null)
        {
            string name = string.IsNullOrWhiteSpace(logDirectoryName) ? DefaultLogDirectoryName : logDirectoryName;
            if (OperatingSystem.IsWindows())
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "FittyAr", "PolyglotCLI", name);
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
        }

        /// <summary>
        /// Configura el logger GLOBAL según el AppConfig. Idempotente: sólo
        /// la primera llamada (o una con <paramref name="logDirOverride"/>)
        /// lo reconstruye. Para logs por-job usar <see cref="BeginJobLog"/>.
        /// </summary>
        public static void Initialize(AppConfig config, string? logDirOverride = null)
        {
            if (_isInitialized && string.IsNullOrEmpty(logDirOverride)) return;

            try
            {
                string logDir = string.IsNullOrEmpty(logDirOverride)
                    ? GetDefaultLogDirectory(config.LogDirectory)
                    : logDirOverride;

                var newLogger = BuildLogger(config, logDir, out var fileLevel);
                var previous = Log.Logger;
                Log.Logger = newLogger;
                (previous as IDisposable)?.Dispose();
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

        /// <summary>
        /// Abre un log adicional para un job en <paramref name="jobLogDir"/>
        /// (polyglot.log + un archivo por proceso). Los mensajes siguen yendo
        /// también al logger global. Al hacer Dispose del valor devuelto se
        /// cierra el archivo del job y se restaura el estado anterior.
        /// </summary>
        public static IDisposable BeginJobLog(AppConfig config, string jobLogDir)
        {
            Logger? jobLogger = null;
            try
            {
                jobLogger = BuildLogger(config, jobLogDir, out _);
            }
            catch (Exception ex)
            {
                Log.Warning("No se pudo crear el log del job en {Dir}: {Error}", jobLogDir, ex.Message);
            }

            var previous = _jobLogger.Value;
            _jobLogger.Value = jobLogger;
            return new JobLogScope(previous, jobLogger);
        }

        private static Logger BuildLogger(AppConfig config, string logDir, out LogEventLevel fileLevel)
        {
            Directory.CreateDirectory(logDir);

            // Parse minimum level for file logs
            if (!Enum.TryParse<LogEventLevel>(config.LogLevelFile, true, out fileLevel))
            {
                fileLevel = LogEventLevel.Debug;
            }

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Is(fileLevel)
                .Enrich.FromLogContext()
                .Enrich.With(new ThreadIdEnricher());

            // 1. Unified log file sink (polyglot.log) - holds all logs
            loggerConfig.WriteTo.File(
                Path.Combine(logDir, "polyglot.log"),
                outputTemplate: LogOutputTemplate,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedLogFiles);

            // 2. Process-specific split log files (e.g. logs/extraction.log)
            loggerConfig.WriteTo.Map(
                "ProcessType",
                "System",
                (processType, wt) => {
                    // Sanitize process name for filename compatibility
                    string safeName = string.Join("_", processType.Split(Path.GetInvalidFileNameChars())).ToLowerInvariant();
                    wt.File(
                        Path.Combine(logDir, $"{safeName}.log"),
                        outputTemplate: ProcessLogOutputTemplate,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: RetainedLogFiles);
                });

            return loggerConfig.CreateLogger();
        }

        private sealed class JobLogScope : IDisposable
        {
            private readonly ILogger? _previous;
            private readonly Logger? _logger;
            private bool _disposed;

            public JobLogScope(ILogger? previous, Logger? logger)
            {
                _previous = previous;
                _logger = logger;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _jobLogger.Value = _previous;
                _logger?.Dispose();
            }
        }

        private static void Write(LogEventLevel level, string message, Exception? ex = null)
        {
            Log.Write(level, ex, message);
            _jobLogger.Value?.Write(level, ex, message);
        }

        public static IDisposable BeginProcess(string processName)
        {
            return LogContext.PushProperty("ProcessType", processName);
        }

        public static void Shutdown()
        {
            Log.CloseAndFlush();
        }

        public static void Debug(string message) 
        { 
            Write(LogEventLevel.Debug, message); 
            OnLogMessage?.Invoke(message, LogLevel.DEBUG); 
        }
        public static void Info(string message) 
        { 
            Write(LogEventLevel.Information, message); 
            OnLogMessage?.Invoke(message, LogLevel.INFO); 
        }
        public static void Warn(string message) 
        { 
            Write(LogEventLevel.Warning, message); 
            OnLogMessage?.Invoke(message, LogLevel.WARN); 
        }
        public static void Error(string message, Exception? ex = null) 
        { 
            Write(LogEventLevel.Error, message, ex); 
            OnLogMessage?.Invoke(message + (ex != null ? $" - {ex.Message}" : ""), LogLevel.ERROR); 
        }
        public static void Fatal(string message, Exception? ex = null) 
        { 
            Write(LogEventLevel.Fatal, message, ex); 
            OnLogMessage?.Invoke(message + (ex != null ? $" - {ex.Message}" : ""), LogLevel.FATAL); 
        }

        public static void InfoConsole(string message, ConsoleColor? color = null)
        {
            Write(LogEventLevel.Information, message);
            OnLogMessage?.Invoke(message, LogLevel.INFO);
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
            Write(LogEventLevel.Warning, message);
            OnLogMessage?.Invoke(message, LogLevel.WARN);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        public static void ErrorConsole(string message, Exception? ex = null)
        {
            Write(LogEventLevel.Error, message, ex);
            OnLogMessage?.Invoke(message + (ex != null ? $" - {ex.Message}" : ""), LogLevel.ERROR);
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
