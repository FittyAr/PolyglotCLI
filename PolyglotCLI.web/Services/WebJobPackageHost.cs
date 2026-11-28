using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using PolyglotCLI.web.Localization;
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
                    Loc.T("Host.JobFolderMissing", job.JobId));
            }

            if (string.Equals(job.Status, "InProgress", StringComparison.OrdinalIgnoreCase))
            {
                bool? confirm = await _dialog.Confirm(
                    Loc.T("Host.ExportInProgressConfirm", job.JobId),
                    Loc.T("Host.ExportInProgressTitle"),
                    new ConfirmOptions { OkButtonText = Loc.T("Host.ExportAnyway"), CancelButtonText = Loc.T("Common.Cancel") });

                if (confirm != true)
                {
                    return Loc.T("Host.ExportCancelled");
                }
            }

            string downloadUrl = $"api/jobs/{Uri.EscapeDataString(job.JobId)}/package";
            _nav.NavigateTo(downloadUrl, forceLoad: true);
            // Centinela, no se muestra: History.razor oculta los mensajes
            // que contienen "Descarga iniciada". No traducir.
            return $"Descarga iniciada: {job.JobId}{JobPackageService.PackageExtension}";
        }

        public async Task<string?> ImportJobPackageAsync()
        {
            // El diálogo cierra con DialogService.Close(newJobId) en éxito (string)
            // y con DialogService.Close() en cancelación (null/undefined).
            string apiUrl = $"{_nav.BaseUri.TrimEnd('/')}/api/jobs/import";

            dynamic? resultObj = await _dialog.OpenAsync<Components.Dialogs.ImportJobDialog>(
                Loc.T("Host.ImportDialogTitle"),
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
