using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace PolyglotCLI.web.Services
{
    public class WebJobPackageHost : IJobPackageHost
    {
        private readonly NavigationManager _nav;
        private readonly DialogService _dialog;

        public WebJobPackageHost(NavigationManager nav, DialogService dialog)
        {
            _nav = nav;
            _dialog = dialog;
        }

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

            if (string.Equals(job.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
            {
                bool? confirm = await _dialog.Confirm(
                    $"El trabajo '{job.JobId}' aún está en progreso. Exportarlo ahora generará un paquete parcial: los archivos en 'data/' pueden estar a medio escribir y el zip incluirá una nota de exportación parcial.\n\n¿Deseas continuar?",
                    "Exportar trabajo en curso",
                    new ConfirmOptions { OkButtonText = "Exportar de todos modos", CancelButtonText = "Cancelar" });

                if (confirm != true)
                {
                    return "Exportación cancelada por el usuario.";
                }
            }

            string downloadUrl = $"api/jobs/{Uri.EscapeDataString(job.JobId)}/package";
            _nav.NavigateTo(downloadUrl, forceLoad: true);
            return $"Descarga iniciada: {job.JobId}{JobPackageService.PackageExtension}";
        }

        public async Task<string?> ImportJobPackageAsync()
        {
            // El diálogo cierra con DialogService.Close(newJobId) en éxito (string)
            // y con DialogService.Close() en cancelación (null/undefined).
            string apiUrl = $"{_nav.BaseUri.TrimEnd('/')}/api/jobs/import";

            dynamic? resultObj = await _dialog.OpenAsync<Components.Dialogs.ImportJobDialog>(
                "Importar trabajo (.zpg)",
                new Dictionary<string, object?>
                {
                    { "ApiUrl", apiUrl }
                },
                new DialogOptions { Width = "520px", Resizable = true, Draggable = true, CloseDialogOnEsc = true });

            if (resultObj is null) return null;
            string? result = resultObj.ToString();
            return string.IsNullOrEmpty(result) ? null : result;
        }
    }
}
