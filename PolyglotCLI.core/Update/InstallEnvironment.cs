using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace PolyglotCLI.Update
{
    /// <summary>
    /// Detecta el tipo de instalación actual de PolyglotCLI para decidir
    /// si el sistema de auto-update debe actuar o no.
    ///
    /// Hay dos modos soportados:
    ///   - <b>MSIX (Microsoft Store)</b>: la app vive en una carpeta de
    ///     sólo-lectura bajo <c>C:\Program Files\WindowsApps\</c> y la
    ///     actualización la gestiona el propio Store. En este modo el
    ///     sistema de update <b>no debe hacer nada</b> (ni consultar
    ///     GitHub, ni notificar).
    ///   - <b>Inno Setup (instalador .exe)</b>: la app vive en
    ///     <c>%ProgramFiles%\FittyAr\PolyglotCLI\Server\serverapp</c> y
    ///     se puede actualizar re-ejecutando el nuevo instalador con
    ///     flags silenciosos, lo que respeta la selección de componentes
    ///     guardada en el registro.
    /// </summary>
    public static class InstallEnvironment
    {
        /// <summary>
        /// Raíz de instalación de Inno Setup. <c>null</c> si no se
        /// detectó (p.ej. durante el desarrollo con <c>dotnet run</c>).
        /// </summary>
        public static string? InnoInstallRoot { get; } = ResolveInnoInstallRoot();

        /// <summary>
        /// <c>true</c> si la app se está ejecutando desde un paquete MSIX
        /// (Microsoft Store). En este caso el sistema de auto-update
        /// queda deshabilitado por completo.
        /// </summary>
        public static bool IsMsixInstalled { get; } = DetectMsix();

        /// <summary>
        /// <c>true</c> si la app se está ejecutando desde una instalación
        /// estándar de Inno Setup (es decir, bajo
        /// <c>%ProgramFiles%\FittyAr\PolyglotCLI</c>). Útil para que la
        /// UI muestre la ruta de instalación y la opción de "abrir carpeta".
        /// </summary>
        public static bool IsInnoInstalled => InnoInstallRoot is not null;

        /// <summary>
        /// <c>true</c> si el sistema de auto-update debe ejecutarse. Es
        /// la combinación que el usuario pidió: <b>solo instalaciones
        /// .exe (Inno)</b>; las MSIX las gestiona el Store.
        /// </summary>
        public static bool CanSelfUpdate => IsInnoInstalled && !IsMsixInstalled;

        /// <summary>
        /// Detección de MSIX: el ejecutable vive dentro de la carpeta
        /// protegida <c>WindowsApps</c> propiedad de
        /// <c>Microsoft\Windows\CurrentVersion\Appx</c>. Esta heurística
        /// funciona en .NET 10 (no requiere referencias a WinRT).
        /// </summary>
        private static bool DetectMsix()
        {
            try
            {
                // ProcessPath es la fuente más fiable en .NET 6+; cae a
                // MainModule si está vacía.
                string? path = Environment.ProcessPath;
                if (string.IsNullOrEmpty(path))
                {
                    path = Process.GetCurrentProcess().MainModule?.FileName;
                }
                if (string.IsNullOrEmpty(path)) return false;

                return path.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Detecta la raíz de instalación de Inno Setup buscando la
        /// estructura de carpetas que el .iss genera:
        /// <c>%ProgramFiles%\FittyAr\PolyglotCLI\Server\serverapp\PolyglotCLI.exe</c>.
        /// Devuelve <c>null</c> si no se encuentra (modo dev o MSIX).
        /// </summary>
        private static string? ResolveInnoInstallRoot()
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;

                // 1. Inferencia a partir de la ruta del ejecutable actual
                string? exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe))
                {
                    exe = Process.GetCurrentProcess().MainModule?.FileName;
                }
                if (!string.IsNullOrEmpty(exe))
                {
                    // /Server/serverapp/PolyglotCLI.exe -> raíz PolyglotCLI
                    var dir = new DirectoryInfo(exe);
                    // Subir hasta 4 niveles: exe -> serverapp -> Server -> PolyglotCLI
                    for (int i = 0; i < 4 && dir is not null; i++, dir = dir.Parent)
                    {
                        if (string.Equals(dir.Name, "PolyglotCLI", StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(dir.Parent?.Name, "FittyAr", StringComparison.OrdinalIgnoreCase))
                        {
                            return dir.FullName;
                        }
                    }
                }

                // 2. Fallback: ruta estándar
                string? programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
                if (!string.IsNullOrEmpty(programFiles))
                {
                    string std = Path.Combine(programFiles, "FittyAr", "PolyglotCLI");
                    if (Directory.Exists(std)) return std;
                }
            }
            catch
            {
                // No hacer nada: el consumidor verá CanSelfUpdate == false
            }
            return null;
        }
    }
}
