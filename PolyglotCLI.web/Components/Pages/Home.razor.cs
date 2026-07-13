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

    protected CommandLineOptions options = new CommandLineOptions();
    protected bool isProcessing = false;
    protected List<LogEntry> logs = new List<LogEntry>();
    protected List<string> availableModels = new List<string>();
    
    // File Browser State
    protected List<string> drives = new List<string>();
    protected string selectedDrive = "";
    protected string currentDirectory = "";
    protected List<FileSystemItem> fileSystemItems = new List<FileSystemItem>();
    protected bool allSelected = false;
    protected IList<FileSystemItem> selectedRows = new List<FileSystemItem>();

    protected override async Task OnInitializedAsync()
    {
        options.TargetLanguage = Config.TargetLanguage;
        options.OutputDirectory = Config.OutputDirectory;
        options.SelectedFormat = Config.DefaultOutputFormat;
        options.Verify = Config.EnableReview;
        options.GenerateDoc = !string.IsNullOrEmpty(Config.DefaultOutputFormat);
        options.AdditionalPrompt = Config.AdditionalPrompt;

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
        
        InvokeAsync(() => {
            logs.Add(new LogEntry { Message = $"[{DateTime.Now:HH:mm:ss}] {message}", CssClass = cssClass });
            if(logs.Count > 100) logs.RemoveAt(0); // keep max 100 lines
            StateHasChanged();
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

        Config.LastScanDirectory = currentDirectory;
        Config.AdditionalPrompt = options.AdditionalPrompt?.Trim();
        Config.DefaultOutputFormat = options.SelectedFormat;

        var outputFormats = new List<string>();
        if (Config.SaveMarkdown) outputFormats.Add("md");
        if (!string.IsNullOrEmpty(options.SelectedFormat)) outputFormats.Add(options.SelectedFormat);
        if (outputFormats.Count == 0) outputFormats.Add("md");
        Config.OutputFormats = string.Join(",", outputFormats);

        try
        {
            Config.Save();
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
        if (string.IsNullOrEmpty(rawInput)) return;

        string url = Config.ApiUrl;
        string model = Config.DefaultModel ?? "";
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(model))
        {
            NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Error de Configuración", Detail = "Debe configurar la URL de API y el Modelo de Traducción en Configuración primero." });
            return;
        }

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

        string url = Config.ApiUrl;
        string model = Config.DefaultModel ?? "";
        string visionModel = Config.DefaultVisionModel ?? "";
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(model))
        {
            NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Error de Configuración", Detail = "Debe configurar la URL de API y el Modelo en Configuración primero." });
            return;
        }

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
        if (selectedFiles.Count == 0)
        {
            AppLogger.Warn("No hay archivos seleccionados para traducir.");
            return;
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

        isProcessing = true;
        logs.Clear();
        StateHasChanged();

        try
        {
            int exitCode = await Task.Run(() => TranslationOrchestrator.ExecuteAsync(args, Config));
            
            if(exitCode == 0)
                AppLogger.Info($"Traducción finalizada con éxito. Salida en: {Config.OutputDirectory}");
            else
                AppLogger.Error($"Traducción falló con código {exitCode}.");
        }
        catch(Exception ex)
        {
            AppLogger.Fatal($"Error crítico: {ex.Message}");
        }
        finally
        {
            isProcessing = false;
            StateHasChanged();
        }
    }
}
