using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Radzen;
using PolyglotCLI;
using PolyglotCLI.web.Components.Dialogs;

namespace PolyglotCLI.web.Components.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Inject]
    protected PolyglotCLI.AppConfig Config { get; set; } = default!;

    [Inject]
    protected DialogService DialogService { get; set; } = default!;

    [Inject]
    protected NotificationService NotificationService { get; set; } = default!;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    protected Microsoft.JSInterop.IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter]
    [SupplyParameterFromQuery(Name = "resumeJobId")]
    public string? ResumeJobId { get; set; }

    protected CommandLineOptions options = new CommandLineOptions();
    protected bool isProcessing = false;
    protected bool isConsoleExpanded = false;
    protected bool isAutoscrollEnabled = true;
    protected System.Threading.CancellationTokenSource? cts;
    protected List<LogEntry> logs = new List<LogEntry>();
    protected List<string> availableModels = new List<string>();
    private bool hasResumed = false;
    
    // File Browser State
    protected List<string> drives = new List<string>();
    protected string selectedDrive = "";
    protected string currentDirectory = "";
    protected List<FileSystemItem> fileSystemItems = new List<FileSystemItem>();
    protected bool allSelected = false;
    protected IList<FileSystemItem> selectedRows = new List<FileSystemItem>();

    protected override async Task OnInitializedAsync()
    {
        Config.Reload();
        options.TargetLanguage = Config.TargetLanguage;
        options.OutputDirectory = Config.OutputDirectory;
        options.SelectedFormat = Config.DefaultOutputFormat;
        options.Verify = Config.EnableReview;
        options.GenerateDoc = !string.IsNullOrEmpty(Config.DefaultOutputFormat);
        options.AdditionalPrompt = Config.AdditionalPrompt;
        options.Transcribe = true;
        options.Translate = true;
        options.Debug = Config.Debug;

        // Initialize drives
        try
        {
            drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => d.Name)
                .ToList();
        }
        catch
        {
            drives = new List<string>();
        }

        // Set starting directory
        currentDirectory = Config.LastScanDirectory;
        if (string.IsNullOrEmpty(currentDirectory) || !Directory.Exists(currentDirectory))
        {
            currentDirectory = Directory.GetCurrentDirectory();
        }
        currentDirectory = Path.GetFullPath(currentDirectory);

        if (drives.Count > 0)
        {
            var root = Path.GetPathRoot(currentDirectory);
            selectedDrive = drives.FirstOrDefault(d => d.Equals(root, StringComparison.OrdinalIgnoreCase)) ?? drives[0];
        }

        LoadDirectory(currentDirectory);

        AppLogger.OnLogMessage += HandleLog;
    }

    public void Dispose()
    {
        AppLogger.OnLogMessage -= HandleLog;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(ResumeJobId) && !hasResumed && !isProcessing)
        {
            hasResumed = true;
            // Esperar un momento breve para asegurar que el callback de logs esté registrado y el componente renderizado
            await Task.Delay(500);

            var args = new CommandLineOptions
            {
                ResumeJobId = ResumeJobId,
                ApiUrl = Config.ApiUrl,
                ModelName = Config.DefaultModel,
                VisionModelName = Config.DefaultVisionModel,
                Verify = Config.EnableReview
            };

            // Ejecutar la reanudación
            await RunTranslation(args);
        }
    }

    private void HandleLog(string message, LogLevel level)
    {
        string cssClass = level switch {
            LogLevel.DEBUG => "log-debug",
            LogLevel.INFO => "log-info",
            LogLevel.WARN => "log-warn",
            LogLevel.ERROR => "log-error",
            LogLevel.FATAL => "log-fatal",
            _ => "log-info"
        };
        
        InvokeAsync(async () => {
            logs.Add(new LogEntry { Message = $"[{DateTime.Now:HH:mm:ss}] {message}", CssClass = cssClass });
            if(logs.Count > 100) logs.RemoveAt(0); // keep max 100 lines
            StateHasChanged();
            
            if (isAutoscrollEnabled)
            {
                try
                {
                    // Delay breve para asegurar que el navegador haya renderizado el nuevo elemento en el DOM
                    await Task.Delay(10);
                    await JSRuntime.InvokeVoidAsync("eval", "var el = document.getElementById('consoleOutput'); if(el) el.scrollTop = el.scrollHeight;");
                }
                catch {}
            }
        });
    }

    protected void LoadDirectory(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return;
            }

            var resolvedPath = Path.GetFullPath(path);
            var items = new List<FileSystemItem>();

            // Add parent directory item ".." if not at root
            var root = Path.GetPathRoot(resolvedPath);
            if (!resolvedPath.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                var parent = Directory.GetParent(resolvedPath);
                if (parent != null)
                {
                    items.Add(new FileSystemItem
                    {
                        IsDirectory = true,
                        Name = "..",
                        FullPath = parent.FullName
                    });
                }
            }

            // Get subdirectories
            foreach (var dir in Directory.GetDirectories(resolvedPath))
            {
                items.Add(new FileSystemItem
                {
                    IsDirectory = true,
                    Name = Path.GetFileName(dir),
                    FullPath = dir
                });
            }

            // Get files with supported extensions
            var supportedExtensions = new HashSet<string>(Config.SupportedInputExtensions ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            
            foreach (var file in Directory.GetFiles(resolvedPath))
            {
                string ext = Path.GetExtension(file);
                if (supportedExtensions.Contains(ext))
                {
                    bool isImage = ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                   ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                   ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                                   ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
                                   ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
                    
                    items.Add(new FileSystemItem
                    {
                        IsDirectory = false,
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        Extension = ext.ToLowerInvariant(),
                        Mode = isImage ? "image" : "text",
                        PageRange = "all",
                        IsSelected = false
                    });
                }
            }

            fileSystemItems = items;
            currentDirectory = resolvedPath;
            allSelected = false;
            selectedRows = new List<FileSystemItem>(); // Clear highlighted items on directory change

            // Save last scanned directory in config
            Config.LastScanDirectory = currentDirectory;
            Config.Save();
            
            // Set drive selection to match current path
            if (drives.Count > 0)
            {
                var currentRoot = Path.GetPathRoot(currentDirectory);
                var match = drives.FirstOrDefault(d => d.Equals(currentRoot, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    selectedDrive = match;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"No se pudo acceder al directorio {path}: {ex.Message}");
        }
    }

    protected void ChangeDrive(object value)
    {
        if (value is string driveName && Directory.Exists(driveName))
        {
            LoadDirectory(driveName);
        }
    }

    protected void SelectAllFiles(bool select)
    {
        allSelected = select;
        foreach (var item in fileSystemItems)
        {
            if (!item.IsDirectory)
            {
                item.IsSelected = select;
            }
        }
    }

    protected async Task SavePresets()
    {
        if (string.IsNullOrEmpty(currentDirectory) || !Directory.Exists(currentDirectory))
        {
            NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Warning, Summary = "Validación", Detail = "Por favor especifique un directorio de exploración válido." });
            return;
        }

        try
        {
            Config.SavePresets(
                currentDirectory,
                options.AdditionalPrompt?.Trim(),
                options.Verify,
                !string.IsNullOrEmpty(options.SelectedFormat),
                options.SelectedFormat
            );
            NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Success, Summary = "Preajustes Guardados", Detail = "Los ajustes por defecto han sido guardados en config.json" });
        }
        catch (Exception ex)
        {
            NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Error al Guardar", Detail = ex.Message });
        }
    }

    protected async Task ImprovePrompt()
    {
        string rawInput = options.AdditionalPrompt?.Trim() ?? "";
        
        var promptValidation = JobValidator.ValidatePromptImprovementSettings(Config, rawInput);
        if (!promptValidation.IsValid)
        {
            NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Error de Configuración", Detail = promptValidation.ErrorMessage ?? "Error al validar la configuración para mejorar prompt." });
            return;
        }

        string url = Config.ApiUrl;
        string model = Config.DefaultModel ?? "";

        isProcessing = true;
        StateHasChanged();
        
        string? improvedResult = null;
        try
        {
            improvedResult = await Task.Run(() => PromptHelperService.ImprovePromptAsync(
                rawInput, 
                url, 
                model, 
                Config.PromptImproveTimeoutSeconds, 
                Config.Temperature
            ));
        }
        catch (Exception ex)
        {
            NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Error al Mejorar Prompt", Detail = ex.Message });
        }
        finally
        {
            isProcessing = false;
            StateHasChanged();
        }

        if (!string.IsNullOrEmpty(improvedResult))
        {
            var result = await DialogService.OpenAsync<ImprovedPromptDialog>("Previsualización de Prompt Mejorado por IA", new Dictionary<string, object?>
            {
                { "RawInput", rawInput },
                { "ImprovedResult", improvedResult }
            }, new DialogOptions { Width = "750px" });

            if (result == true)
            {
                options.AdditionalPrompt = improvedResult;
                NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Success, Summary = "Éxito", Detail = "Prompt actualizado con la versión mejorada por IA." });
            }
        }
    }

    protected async Task AnalyzeFilePrompt()
    {
        if (selectedRows.Count != 1 || selectedRows[0].IsDirectory) return;
        var file = selectedRows[0];

        var fileAnalysisValidation = JobValidator.ValidateFileAnalysisSettings(Config, 0, 1);
        if (!fileAnalysisValidation.IsValid)
        {
            NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Error de Configuración", Detail = fileAnalysisValidation.ErrorMessage ?? "Error al validar la configuración para analizar archivo." });
            return;
        }

        string url = Config.ApiUrl;
        string model = Config.DefaultModel ?? "";
        string visionModel = Config.DefaultVisionModel ?? "";

        isProcessing = true;
        var progressState = new ProgressState { Status = "Preparando análisis..." };
        
        // Show progress dialog
        var progressDialog = DialogService.OpenAsync<ProgressDialog>("Analizando Archivo", new Dictionary<string, object?>
        {
            { "State", progressState }
        }, new DialogOptions { ShowTitle = true, ShowClose = false, Width = "400px" });

        string? improvedPromptResult = null;
        try
        {
            improvedPromptResult = await Task.Run(() => PromptHelperService.GenerateContextPromptAsync(
                file.FullPath,
                file.Mode,
                file.PageRange,
                Config,
                url,
                model,
                visionModel,
                (status) => {
                    progressState.UpdateStatus(status);
                }
            ));
        }
        catch (Exception ex)
        {
            NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Error al Analizar Archivo", Detail = ex.Message });
        }
        finally
        {
            DialogService.Close(); // Close progress dialog
            isProcessing = false;
            StateHasChanged();
        }

        if (!string.IsNullOrEmpty(improvedPromptResult))
        {
            var result = await DialogService.OpenAsync<AnalyzeFilePromptDialog>("Previsualización de Prompt Basado en Contexto de Archivo", new Dictionary<string, object?>
            {
                { "FileName", file.Name },
                { "ImprovedPromptResult", improvedPromptResult }
            }, new DialogOptions { Width = "650px" });

            if (result == true)
            {
                options.AdditionalPrompt = improvedPromptResult;
                NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Success, Summary = "Éxito", Detail = "Instrucciones de traducción actualizadas con el análisis del archivo." });
            }
        }
    }

    protected async Task RunTranslation(CommandLineOptions args)
    {
        var selectedFiles = fileSystemItems.Where(i => !i.IsDirectory && i.IsSelected).ToList();

        if (string.IsNullOrEmpty(args.ResumeJobId))
        {
            if (selectedFiles.Count == 0)
            {
                AppLogger.Warn("No hay archivos seleccionados para traducir.");
                NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Warning, Summary = "Validación", Detail = "Debe seleccionar al menos un archivo para traducir." });
                return;
            }

            // 1. Validar configuraciones generales del trabajo con el validador del Core
            var jobSettingsValidation = JobValidator.ValidateJobSettings(Config, currentDirectory);
            if (!jobSettingsValidation.IsValid)
            {
                AppLogger.Error(jobSettingsValidation.ErrorMessage ?? "Configuración de trabajo inválida.");
                NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Configuración Inválida", Detail = jobSettingsValidation.ErrorMessage ?? "Error de configuración de trabajo." });
                return;
            }

            // 2. Validar requerimiento de modelo de visión para OCR con el validador del Core
            var selectableFiles = selectedFiles.Select(i => new SelectableFile
            {
                FullPath = i.FullPath,
                IsSelected = true,
                Mode = i.Mode,
                PageRange = i.PageRange
            }).ToList();

            var ocrValidation = JobValidator.ValidateOcrModelRequirement(Config, selectableFiles);
            if (!ocrValidation.IsValid)
            {
                AppLogger.Error(ocrValidation.ErrorMessage ?? "Error de validación del modelo de visión.");
                NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Modelo de Visión Requerido", Detail = ocrValidation.ErrorMessage ?? "Se requiere un modelo de visión para OCR." });
                return;
            }

            // 3. Validar rangos de páginas para PDFs
            foreach (var file in selectedFiles)
            {
                if (file.Extension != null && file.Extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    if (!CommandLineOptions.IsValidPageRange(file.PageRange))
                    {
                        string errorMsg = $"El rango de páginas '{file.PageRange}' para el archivo '{file.Name}' no es válido.";
                        AppLogger.Error(errorMsg);
                        NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Rango de páginas inválido", Detail = errorMsg });
                        return;
                    }
                }
            }

            args.Files = selectedFiles.Select(i => i.FullPath).ToList();
            args.DocumentTargets = selectedFiles.Select(i => new DocumentTarget
            {
                FilePath = i.FullPath,
                Mode = i.Mode,
                PageRange = i.PageRange,
                MaxCharactersPerChunk = Config.MaxCharactersPerChunk,
                ChunkOverlapCharacters = Config.ChunkOverlapCharacters
            }).ToList();
            
            args.ApiUrl = Config.ApiUrl;
            args.ModelName = Config.DefaultModel;
            args.VisionModelName = Config.DefaultVisionModel;
            args.GenerateDoc = !string.IsNullOrWhiteSpace(args.SelectedFormat);
            args.Verify = Config.EnableReview;
            args.Transcribe = options.Transcribe;
            args.Translate = options.Translate;
            args.Debug = options.Debug;
        }
        else
        {
            AppLogger.Info($"Iniciando reanudación interactiva del trabajo '{args.ResumeJobId}'...");
        }

        isProcessing = true;
        logs.Clear();
        cts = new System.Threading.CancellationTokenSource();
        StateHasChanged();

        try
        {
            int exitCode = await Task.Run(() => TranslationOrchestrator.ExecuteAsync(args, Config, cts.Token));
            
            if (cts.IsCancellationRequested)
            {
                AppLogger.WarnConsole("\n[SYSTEM] Traducción detenida por el usuario.");
            }
            else if (exitCode == 0)
            {
                AppLogger.Info($"Traducción finalizada con éxito. Salida en: {Config.OutputDirectory}");
            }
            else
            {
                AppLogger.Error($"Traducción falló con código {exitCode}.");
            }
        }
        catch (OperationCanceledException)
        {
            AppLogger.WarnConsole("\n[SYSTEM] El proceso fue cancelado por el usuario.");
        }
        catch (Exception ex)
        {
            if (cts.IsCancellationRequested || ex is OperationCanceledException || ex.InnerException is OperationCanceledException)
            {
                AppLogger.WarnConsole("\n[SYSTEM] El proceso fue cancelado por el usuario.");
            }
            else
            {
                AppLogger.Fatal($"Error crítico: {ex.Message}");
            }
        }
        finally
        {
            isProcessing = false;
            cts?.Dispose();
            cts = null;
            StateHasChanged();
        }
    }

    protected void CancelProcess()
    {
        if (cts != null)
        {
            AppLogger.WarnConsole("\n[USER] Solicitando detención inmediata. Cancelando tareas...");
            cts.Cancel();
        }
    }

    protected void ToggleConsoleExpansion()
    {
        isConsoleExpanded = !isConsoleExpanded;
        StateHasChanged();
    }

    protected void ToggleAutoscroll()
    {
        isAutoscrollEnabled = !isAutoscrollEnabled;
        StateHasChanged();
    }
}
