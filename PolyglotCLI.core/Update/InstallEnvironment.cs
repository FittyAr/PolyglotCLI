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
        /// Código de retorno de <c>GetCurrentPackageFullName</c> cuando el
        /// proceso NO tiene identidad de paquete.
        /// </summary>
        private const int AppModelErrorNoPackage = 15700;

        /// <summary>
        /// Código de retorno cuando el buffer es insuficiente: implica que
        /// SÍ hay identidad de paquete (el nombre no entra en 0 chars).
        /// </summary>
        private const int ErrorInsufficientBuffer = 122;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

        /// <summary>
        /// Detección de MSIX. En Windows 8+ se consulta la identidad de
        /// paquete del proceso con <c>GetCurrentPackageFullName</c>
        /// (fuente autoritativa: también cubre instalaciones MSIX fuera de
        /// WindowsApps). Si la API no está disponible (otro SO, Windows 7,
        /// P/Invoke fallido) se cae a la heurística de ruta
        /// <c>\WindowsApps\</c>.
        /// </summary>
        private static bool DetectMsix()
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    int length = 0;
                    int rc = GetCurrentPackageFullName(ref length, null);
                    if (rc == AppModelErrorNoPackage) return false;
                    if (rc == ErrorInsufficientBuffer || rc == 0) return true;
                }
                catch (Exception ex) when (ex is EntryPointNotFoundException || ex is DllNotFoundException)
                {
                    // API no disponible: heurística de ruta.
                }
            }
            return DetectMsixByPath();
        }

        private static bool DetectMsixByPath()
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
        /// Devuelve <c>null</c> si no se encuentra (modo dev, portable o MSIX).
        /// Sólo se mira la ruta del ejecutable ACTUAL: un fallback a
        /// "%ProgramFiles%\FittyAr\PolyglotCLI existe" haría que una copia
        /// de desarrollo/portable (o la de la Store) se creyera instalación
        /// Inno y se auto-actualizara.
        /// </summary>
        private static string? ResolveInnoInstallRoot()
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;

                // Inferencia a partir de la ruta del ejecutable actual
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

            }
            catch
            {
                // No hacer nada: el consumidor verá CanSelfUpdate == false
            }
            return null;
        }
    }
}
