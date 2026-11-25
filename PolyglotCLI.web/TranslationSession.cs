using System;
using System.Collections.Generic;
using System.Threading;
using PolyglotCLI.web.Components.Pages;

namespace PolyglotCLI.web
{
    /// <summary>
    /// Estado global (proceso) de la traducción en curso. Es compartido por
    /// todos los circuitos/pestañas, así que todo acceso es thread-safe:
    /// <list type="bullet">
    ///   <item>Solo puede haber un trabajo activo (<see cref="TryBegin"/>).</item>
    ///   <item>La consola de logs se alimenta desde una única suscripción a
    ///         <see cref="AppLogger.OnLogMessage"/> (no una por pestaña) y se
    ///         lee siempre como snapshot para no romper el render de otro
    ///         circuito con "Collection was modified".</item>
    /// </list>
    /// </summary>
    public static class TranslationSession
    {
        private const int MaxLogEntries = 100;

        private static readonly object Gate = new();
        private static readonly List<LogEntry> LogBuffer = new();
        private static CancellationTokenSource? _cts;
        private static string? _runningJobId;

        static TranslationSession()
        {
            AppLogger.OnLogMessage += AppendLog;
        }

        /// <summary>Se dispara (desde cualquier hilo) cuando cambia la consola de logs.</summary>
        public static event Action? LogsChanged;

        /// <summary>Se dispara (desde cualquier hilo) cuando un trabajo empieza o termina.</summary>
        public static event Action? StateChanged;

        public static bool IsProcessing
        {
            get { lock (Gate) return _cts != null; }
        }

        /// <summary>JobId del trabajo en curso si se conoce (reanudaciones), o null.</summary>
        public static string? RunningJobId
        {
            get { lock (Gate) return _runningJobId; }
        }

        /// <summary>
        /// Intenta reservar la sesión para un nuevo trabajo. Devuelve false si
        /// ya hay otro en curso (p.ej. lanzado desde otra pestaña).
        /// </summary>
        public static bool TryBegin(string? jobId, out CancellationTokenSource cts)
        {
            lock (Gate)
            {
                if (_cts != null)
                {
                    cts = null!;
                    return false;
                }
                _cts = new CancellationTokenSource();
                _runningJobId = jobId;
                LogBuffer.Clear();
                cts = _cts;
            }
            LogsChanged?.Invoke();
            StateChanged?.Invoke();
            return true;
        }

        /// <summary>Libera la sesión. Solo tiene efecto para el CTS que la reservó.</summary>
        public static void End(CancellationTokenSource cts)
        {
            lock (Gate)
            {
                if (!ReferenceEquals(_cts, cts)) return;
                _cts = null;
                _runningJobId = null;
            }
            cts.Dispose();
            StateChanged?.Invoke();
        }

        /// <summary>Solicita la cancelación del trabajo activo. Devuelve false si no había ninguno.</summary>
        public static bool Cancel()
        {
            lock (Gate)
            {
                if (_cts == null) return false;
                try
                {
                    _cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
                return true;
            }
        }

        /// <summary>Copia inmutable de la consola para renderizar.</summary>
        public static IReadOnlyList<LogEntry> GetLogsSnapshot()
        {
            lock (Gate) return LogBuffer.ToArray();
        }

        private static void AppendLog(string message, LogLevel level)
        {
            string cssClass = level switch
            {
                LogLevel.DEBUG => "log-debug",
                LogLevel.INFO => "log-info",
                LogLevel.WARN => "log-warn",
                LogLevel.ERROR => "log-error",
                LogLevel.FATAL => "log-fatal",
                _ => "log-info"
            };

            lock (Gate)
            {
                LogBuffer.Add(new LogEntry { Message = $"[{DateTime.Now:HH:mm:ss}] {message}", CssClass = cssClass });
                if (LogBuffer.Count > MaxLogEntries)
                {
                    LogBuffer.RemoveRange(0, LogBuffer.Count - MaxLogEntries);
                }
            }
            LogsChanged?.Invoke();
        }
    }
}
