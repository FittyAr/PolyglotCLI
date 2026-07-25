using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using PolyglotCLI;
using PolyglotCLI.Update;

namespace PolyglotCLI.web.Services
{
    /// <summary>
    /// Servicio de fondo que consulta periódicamente la última release de
    /// PolyglotCLI en GitHub. Solo se activa cuando
    /// <see cref="InstallEnvironment.CanSelfUpdate"/> es <c>true</c> (es
    /// decir, instalaciones Inno Setup / .exe). En MSIX no hace nada.
    ///
    /// Cuando detecta una nueva versión, dispara el evento
    /// <see cref="OnUpdateAvailable"/> para que la UI (componentes Razor)
    /// muestre un toast de Radzen con el botón "Actualizar ahora".
    /// </summary>
    public sealed class UpdateHostedService : BackgroundService
    {
        /// <summary>
        /// Versión actual de PolyglotCLI (sacada del assembly al
        /// construir el servicio, igual que hace AboutConfigTab).
        /// </summary>
        public string CurrentVersion { get; }

        /// <summary>
        /// Última información de update conocida. <c>null</c> si todavía
        /// no se ha hecho ninguna consulta. La UI puede leer este valor
        /// para mostrar el estado sin necesidad de pegarle a la API.
        /// </summary>
        public UpdateInfo? LastCheck { get; private set; }

        /// <summary>
        /// Dispara cuando se detecta una nueva versión. La UI se suscribe
        /// en <c>OnInitialized</c> y muestra un <c>NotificationService</c>
        /// de Radzen.
        /// </summary>
        public event Action<UpdateInfo>? OnUpdateAvailable;

        private readonly AppConfig _config;
        private readonly UpdateService _update;

        public UpdateHostedService(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            CurrentVersion = ResolveCurrentVersion();
            _update = new UpdateService(CurrentVersion);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // En MSIX o sin configuración habilitada, no hacemos nada.
            if (!InstallEnvironment.CanSelfUpdate) return;
            if (!_config.UpdateCheckEnabled) return;

            // Espera inicial: 30s tras arrancar para no pegar a GitHub
            // antes de que la UI esté lista para mostrar la notificación.
            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var info = await _update.CheckForUpdateAsync(stoppingToken).ConfigureAwait(false);
                    LastCheck = info;
                    // Sólo persistir si la marca de tiempo realmente cambió:
                    // evita reescribir config.json cada 6h cuando nada cambió,
                    // y combinado con la escritura atómica de AppConfig.Save
                    // blinda contra un corte a mitad de archivo.
                    var newStamp = DateTime.UtcNow;
                    if (_config.LastUpdateCheckUtc != newStamp)
                    {
                        _config.LastUpdateCheckUtc = newStamp;
                        _config.Save();
                    }

                    if (info.CheckSucceeded && info.IsUpdateAvailable &&
                        info.LatestVersion != _config.DismissedUpdateVersion)
                    {
                        try { OnUpdateAvailable?.Invoke(info); }
                        catch (Exception ex)
                        {
                            AppLogger.Warn($"UpdateHostedService: un suscriptor lanzó {ex.GetType().Name} - {ex.Message}");
                        }
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    AppLogger.Warn($"UpdateHostedService: error al consultar GitHub - {ex.Message}");
                }

                // Espera hasta el próximo chequeo. Mínimo 1h.
                int hours = Math.Max(1, _config.UpdateCheckIntervalHours);
                try { await Task.Delay(TimeSpan.FromHours(hours), stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }

        /// <summary>
        /// Lanza una consulta inmediata (p.ej. cuando el usuario hace
        /// click en "Buscar actualizaciones"). Devuelve la misma
        /// <see cref="UpdateInfo"/> que la próxima ejecución del bucle
        /// habría producido.
        /// </summary>
        public async Task<UpdateInfo> CheckNowAsync(CancellationToken ct = default)
        {
            var info = await _update.CheckForUpdateAsync(ct).ConfigureAwait(false);
            LastCheck = info;
            var newStamp = DateTime.UtcNow;
            if (_config.LastUpdateCheckUtc != newStamp)
            {
                _config.LastUpdateCheckUtc = newStamp;
                _config.Save();
            }
            if (info.CheckSucceeded && info.IsUpdateAvailable)
            {
                OnUpdateAvailable?.Invoke(info);
            }
            return info;
        }

        /// <summary>
        /// Marca una versión como ignorada: el aviso no volverá a
        /// aparecer hasta que salga una versión aún más nueva.
        /// </summary>
        public void DismissUpdate(string version)
        {
            _config.DismissedUpdateVersion = version;
            _config.Save();
        }

        private static string ResolveCurrentVersion()
        {
            var asm = typeof(UpdateHostedService).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                int p = info.IndexOf('+');
                return p > 0 ? info[..p] : info;
            }
            return asm.GetName().Version?.ToString() ?? "1.0.0";
        }
    }
}
