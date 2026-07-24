using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using PolyglotCLI;
using PolyglotCLI.web.Services.JobDetails;
using Radzen;

namespace PolyglotCLI.web.Components.Dialogs.JobDetails.Parts;

public partial class JobPageVerifierTab : ComponentBase
{
    [Parameter]
    public JobManifest Job { get; set; } = default!;

    [Parameter]
    public string JobDir { get; set; } = string.Empty;

    [Parameter]
    public IReadOnlyDictionary<int, string> PendingTranslationEdits { get; set; } = new Dictionary<int, string>();

    [Inject]
    private IJobPageVerifierService VerifierService { get; set; } = default!;

    [Inject]
    private IJobPageEditService EditService { get; set; } = default!;

    [Inject]
    private IJobPageReprocessService ReprocessService { get; set; } = default!;

    [Inject]
    private AppConfig Config { get; set; } = default!;

    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    private List<JsonFileEntry> JsonFiles { get; set; } = new();
    private JsonFileEntry? SelectedJsonEntry { get; set; }
    private List<DocumentPageData> PageDataList { get; set; } = new();
    private DocumentPageData? SelectedPageData { get; set; }
    private bool HasPageImage { get; set; }
    private string? PageImageBase64 { get; set; }
    private bool IsReprocessingPage { get; set; }
    private bool IsEditingTranslation { get; set; } = true;
    private int _pageLoadGeneration;

    private string? SelectedJsonFile => SelectedJsonEntry?.JsonFileName;

    protected override void OnInitialized()
    {
        JsonFiles = VerifierService.ListJsonFiles(JobDir, Job);
    }

    private Task HandleJsonFileSelected(JsonFileEntry entry)
    {
        SelectedJsonEntry = entry;
        SelectedPageData = null;
        PageImageBase64 = null;
        HasPageImage = false;
        IsEditingTranslation = true;

        if (entry == null)
        {
            return Task.CompletedTask;
        }

        PageDataList = VerifierService.LoadPageData(JobDir, entry.JsonFileName);
        if (PageDataList.Count > 0)
        {
            return HandlePageSelected(PageDataList[0]);
        }
        return Task.CompletedTask;
    }

    private Task HandlePageSelected(DocumentPageData page)
    {
        var generation = ++_pageLoadGeneration;
        SelectedPageData = page;
        PageImageBase64 = null;
        HasPageImage = false;

        if (PendingTranslationEdits.TryGetValue(page.PageNumber, out var pendingText))
        {
            SelectedPageData.TranslatedText = pendingText;
        }

        if (string.IsNullOrEmpty(SelectedJsonFile))
        {
            return Task.CompletedTask;
        }

        string docName = SelectedJsonFile.Replace("_data.json", string.Empty, StringComparison.OrdinalIgnoreCase);
        var base64 = VerifierService.LoadPageImageBase64(JobDir, docName, page.PageNumber);
        if (generation != _pageLoadGeneration)
        {
            return Task.CompletedTask;
        }
        if (base64 != null)
        {
            PageImageBase64 = base64;
            HasPageImage = true;
        }
        // Forzamos el re-render para que el cambio de imagen se propague
        // al visor de pan/zoom antes del próximo ciclo interactivo.
        InvokeAsync(StateHasChanged);
        return Task.CompletedTask;
    }

    private Task HandleToggleEditing()
    {
        IsEditingTranslation = !IsEditingTranslation;
        return Task.CompletedTask;
    }

    private async Task HandleTranslationEdited(string newText)
    {
        if (SelectedPageData == null || SelectedJsonEntry == null)
        {
            return;
        }

        try
        {
            var pendingEdits = new Dictionary<int, string>(PendingTranslationEdits)
            {
                [SelectedPageData.PageNumber] = newText
            };
            SelectedPageData.TranslatedText = newText;

            string sourceFileName = SelectedJsonEntry.JsonFileName.Replace("_data.json", string.Empty, StringComparison.OrdinalIgnoreCase);
            var fileManifest = Job.Files.Find(f =>
                Path.GetFileNameWithoutExtension(f.SourceFilePath).Equals(sourceFileName, StringComparison.OrdinalIgnoreCase));

            await EditService.PersistTranslationEditAsync(
                PageDataList,
                pendingEdits,
                JobDir,
                SelectedJsonEntry.JsonFileName,
                Job,
                fileManifest,
                Config);
        }
        catch (Exception ex)
        {
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Error al guardar cambios",
                Detail = ex.Message
            });
        }
    }

    private async Task HandleReprocessPage()
    {
        if (SelectedPageData == null || string.IsNullOrEmpty(SelectedJsonFile))
        {
            return;
        }

        IsReprocessingPage = true;
        StateHasChanged();

        try
        {
            string sourceFileName = SelectedJsonFile.Replace("_data.json", string.Empty, StringComparison.OrdinalIgnoreCase);
            var fileManifest = Job.Files.Find(f =>
                Path.GetFileNameWithoutExtension(f.SourceFilePath).Equals(sourceFileName, StringComparison.OrdinalIgnoreCase));

            if (fileManifest == null)
            {
                NotificationService.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Error,
                    Summary = "Reprocesamiento",
                    Detail = "No se pudo asociar el archivo de datos con el archivo de origen."
                });
                return;
            }

            bool success = await ReprocessService.ReprocessAsync(Job.JobId, fileManifest.SourceFilePath, SelectedPageData.PageNumber, Config);
            if (success)
            {
                NotificationService.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Success,
                    Summary = "Reprocesamiento",
                    Detail = $"Página {SelectedPageData.PageNumber} procesada con éxito."
                });
                await HandleJsonFileSelected(SelectedJsonEntry!);
            }
            else
            {
                NotificationService.Notify(new NotificationMessage
                {
                    Severity = NotificationSeverity.Error,
                    Summary = "Reprocesamiento",
                    Detail = "El proceso finalizó pero no se pudieron actualizar los datos."
                });
            }
        }
        catch (Exception ex)
        {
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Error",
                Detail = ex.Message
            });
        }
        finally
        {
            IsReprocessingPage = false;
            StateHasChanged();
        }
    }

    private Task HandleViewerWarning()
    {
        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Warning,
            Summary = "Visor",
            Detail = "El visor aún no está inicializado."
        });
        return Task.CompletedTask;
    }

    private Task HandleViewerError(string message)
    {
        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Error,
            Summary = "Pan/Zoom",
            Detail = message
        });
        return Task.CompletedTask;
    }
}
