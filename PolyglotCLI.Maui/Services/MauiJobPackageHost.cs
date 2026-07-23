using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Storage;

namespace PolyglotCLI.Maui.Services
{
    /// <summary>
    /// MAUI implementation of IJobPackageHost. Uses native Windows file pickers
    /// (<see cref="FileSaver"/>, <see cref="FilePicker"/>) and writes the .zpg
    /// directly to the user-chosen path (no HTTP server required).
    /// </summary>
    public class MauiJobPackageHost : IJobPackageHost
    {
        public async Task<string> ExportJobPackageAsync(JobManifest job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));

            string jobsRoot = TranslationOrchestrator.GetJobsDirectory();
            string jobDir = Path.Combine(jobsRoot, job.JobId);
            if (!Directory.Exists(jobDir))
            {
                throw new DirectoryNotFoundException(
                    $"La carpeta del trabajo '{job.JobId}' no existe en disco.");
            }

            string suggestedFileName = $"{job.JobId}{JobPackageService.PackageExtension}";

            // Construimos el zip en un MemoryStream para alimentar FileSaver con un flujo.
            using var packageStream = new MemoryStream();
            JobPackageService.ExportJobPackage(jobDir, packageStream);
            packageStream.Position = 0;

            CancellationTokenSource? cts = null;
            try
            {
                cts = new CancellationTokenSource();
                var result = await FileSaver.Default.SaveAsync(suggestedFileName, packageStream, cts.Token);

                if (!result.IsSuccessful)
                {
                    if (result.Exception is OperationCanceledException)
                    {
                        return "Guardado cancelado por el usuario.";
                    }
                    throw new InvalidOperationException(
                        $"No se pudo guardar el archivo: {result.Exception?.Message ?? "operación cancelada"}",
                        result.Exception);
                }

                return $"Guardado en: {result.FilePath}";
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Error al guardar el archivo .zpg: {ex.Message}", ex);
            }
            finally
            {
                cts?.Dispose();
            }
        }

        public async Task<string?> ImportJobPackageAsync()
        {
            string jobsRoot = TranslationOrchestrator.GetJobsDirectory();

            var pickOptions = new PickOptions
            {
                PickerTitle = "Selecciona un trabajo .zpg",
                FileTypes = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".zpg", ".zip" } },
                        { DevicePlatform.macOS, new[] { "zpg", "zip" } },
                        { DevicePlatform.Android, new[] { "application/zip" } },
                        { DevicePlatform.iOS, new[] { "public.archive", "public.zip-archive" } }
                    })
            };

            FileResult? result;
            try
            {
                result = await FilePicker.Default.PickAsync(pickOptions);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "No se pudo abrir el selector de archivos. Comprueba los permisos de la aplicación.", ex);
            }

            if (result == null)
            {
                return null;
            }

            await using var stream = await result.OpenReadAsync();
            return await JobPackageService.ImportJobPackageAsync(stream, jobsRoot);
        }
    }
}
