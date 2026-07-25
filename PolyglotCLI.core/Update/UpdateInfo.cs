using System;

namespace PolyglotCLI.Update
{
    /// <summary>
    /// Resultado de consultar la última release pública de PolyglotCLI en
    /// GitHub. Toda la información se calcula en una sola llamada al
    /// endpoint <c>releases/latest</c> para evitar golpes innecesarios al
    /// rate limit de GitHub (60 req/h sin token).
    /// </summary>
    public sealed class UpdateInfo
    {
        /// <summary>
        /// Versión instalada actualmente (p.ej. "1.1.2"). Nunca vacía.
        /// </summary>
        public string CurrentVersion { get; init; } = string.Empty;

        /// <summary>
        /// Última versión publicada en GitHub (p.ej. "1.2.0"). Vacía si
        /// la consulta falló (sin red, rate limit, etc.).
        /// </summary>
        public string LatestVersion { get; init; } = string.Empty;

        /// <summary>
        /// URL del instalador .exe (asset de la release). Vacía si la
        /// release no incluye el asset esperado.
        /// </summary>
        public string InstallerDownloadUrl { get; init; } = string.Empty;

        /// <summary>
        /// Tamaño del instalador en bytes. 0 si no se conoce.
        /// </summary>
        public long InstallerSizeBytes { get; init; }

        /// <summary>
        /// Digest SHA-256 del instalador publicado por GitHub en el campo
        /// <c>assets[].digest</c> (formato <c>sha256:hex</c>). Vacío si la
        /// release no incluye digest. Se usa en
        /// <see cref="UpdateService.DownloadInstallerAsync"/> para verificar
        /// la integridad del archivo antes de ejecutarlo como admin.
        /// </summary>
        public string Digest { get; init; } = string.Empty;

        /// <summary>
        /// Notas de la release en Markdown. Vacías si la release no tiene.
        /// </summary>
        public string ReleaseNotes { get; init; } = string.Empty;

        /// <summary>
        /// Fecha de publicación de la release (UTC). <c>null</c> si no se
        /// pudo obtener.
        /// </summary>
        public DateTime? PublishedAt { get; init; }

        /// <summary>
        /// <c>true</c> cuando <see cref="LatestVersion"/> es estrictamente
        /// mayor que <see cref="CurrentVersion"/> según una comparación
        /// semántica simple (x.y.z). Tags sin prefijo "v" se aceptan.
        /// </summary>
        public bool IsUpdateAvailable { get; init; }

        /// <summary>
        /// <c>true</c> cuando se pudo consultar la API (haya update o no).
        /// <c>false</c> cuando hubo error de red, rate limit o respuesta
        /// malformada. El campo <see cref="ErrorMessage"/> trae el detalle.
        /// </summary>
        public bool CheckSucceeded { get; init; }

        /// <summary>
        /// Mensaje de error legible cuando <see cref="CheckSucceeded"/> es
        /// <c>false</c>. Vacío en caso contrario.
        /// </summary>
        public string ErrorMessage { get; init; } = string.Empty;

        /// <summary>
        /// Fábrica de "no hay update" para evitar repetir la inicialización
        /// en cada llamada. Se usa cuando la consulta fue exitosa pero la
        /// versión actual ya es la última.
        /// </summary>
        public static UpdateInfo NoUpdate(string currentVersion) => new()
        {
            CurrentVersion = currentVersion,
            CheckSucceeded = true,
            IsUpdateAvailable = false
        };

        /// <summary>
        /// Fábrica de "error de consulta". Se usa cuando falla la red o el
        /// rate limit, para que la UI pueda mostrar un mensaje sin propagar
        /// excepciones.
        /// </summary>
        public static UpdateInfo Failed(string currentVersion, string error) => new()
        {
            CurrentVersion = currentVersion,
            CheckSucceeded = false,
            ErrorMessage = error
        };
    }
}
