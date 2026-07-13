using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Radzen;
using PolyglotCLI;

namespace PolyglotCLI.web.Components.Pages;

public partial class Config : ComponentBase
{
    [Inject]
    protected PolyglotCLI.AppConfig AppConfig { get; set; } = default!;

    [Inject]
    protected NotificationService NotificationService { get; set; } = default!;

    protected string saveMessage = "";
    protected string outputFormatsInput = "";
    protected List<string> availableModels = new List<string>();
    protected List<string> outputFormatOptions = new List<string> { "html", "docx", "odf", "pdf" };
    protected bool isTestingConnection = false;
    protected string? testConnectionResult = null;

    // Prompts files content
    protected string ocrPromptText = "";
    protected string translationPromptText = "";
    protected string reviewPromptText = "";
    protected string promptImproverPromptText = "";

    protected string GetPromptsDirectory()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "prompts");
        if (!Directory.Exists(path))
        {
            path = Path.Combine(Directory.GetCurrentDirectory(), "prompts");
        }
        return path;
    }

    protected override async Task OnInitializedAsync()
    {
        AppConfig.Reload();
        outputFormatsInput = string.Join(", ", AppConfig.SupportedOutputFormats);
        outputFormatOptions = AppConfig.SupportedOutputFormats ?? new List<string> { "html", "docx", "odf", "pdf" };

        // Load prompt files
        string promptsDir = GetPromptsDirectory();
        try { ocrPromptText = await File.ReadAllTextAsync(Path.Combine(promptsDir, "ocr_prompt.md")); } catch {}
        try { translationPromptText = await File.ReadAllTextAsync(Path.Combine(promptsDir, "translation_prompt.md")); } catch {}
        try { reviewPromptText = await File.ReadAllTextAsync(Path.Combine(promptsDir, "review_prompt.md")); } catch {}
        try { promptImproverPromptText = await File.ReadAllTextAsync(Path.Combine(promptsDir, "prompt_improver_prompt.md")); } catch {}

        // Fetch models dynamically from LM Studio
        try {
            using var client = new LmStudioClient(AppConfig.ApiUrl, 3);
            availableModels = await client.GetAvailableModelsAsync();
        }
        catch {
            if (!string.IsNullOrEmpty(AppConfig.DefaultModel))
                availableModels.Add(AppConfig.DefaultModel);
            if (!string.IsNullOrEmpty(AppConfig.DefaultVisionModel) && !availableModels.Contains(AppConfig.DefaultVisionModel))
                availableModels.Add(AppConfig.DefaultVisionModel);
            if (!string.IsNullOrEmpty(AppConfig.ReviewModel) && !availableModels.Contains(AppConfig.ReviewModel))
                availableModels.Add(AppConfig.ReviewModel);
        }
    }

    protected async Task TestConnection()
    {
        await LoadModelsFromServer();
    }

    protected async Task LoadModelsFromServer()
    {
        isTestingConnection = true;
        testConnectionResult = null;
        StateHasChanged();
        
        try
        {
            using var client = new LmStudioClient(AppConfig.ApiUrl, AppConfig.ModelCheckTimeoutSeconds);
            availableModels = await client.GetAvailableModelsAsync();
            NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Success, Summary = "Carga Exitosa", Detail = $"Se cargaron {availableModels.Count} modelos del servidor." });
            testConnectionResult = "Conexión exitosa";
        }
        catch (Exception ex)
        {
            NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Fallo de Conexión", Detail = ex.Message });
            testConnectionResult = $"Fallo: {ex.Message}";
        }
        finally
        {
            isTestingConnection = false;
            StateHasChanged();
        }
    }

    protected async Task SaveConfig(PolyglotCLI.AppConfig args)
    {
        try
        {
            args.SupportedOutputFormats = outputFormatsInput
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            args.Save();

            // Save system prompt files
            string promptsDir = GetPromptsDirectory();
            if (!Directory.Exists(promptsDir))
            {
                Directory.CreateDirectory(promptsDir);
            }
            try { await File.WriteAllTextAsync(Path.Combine(promptsDir, "ocr_prompt.md"), ocrPromptText ?? ""); } catch {}
            try { await File.WriteAllTextAsync(Path.Combine(promptsDir, "translation_prompt.md"), translationPromptText ?? ""); } catch {}
            try { await File.WriteAllTextAsync(Path.Combine(promptsDir, "review_prompt.md"), reviewPromptText ?? ""); } catch {}
            try { await File.WriteAllTextAsync(Path.Combine(promptsDir, "prompt_improver_prompt.md"), promptImproverPromptText ?? ""); } catch {}

            saveMessage = "Configuración guardada correctamente!";
            NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Success, Summary = "Éxito", Detail = "Configuración guardada correctamente." });
            
            await Task.Delay(3000);
            saveMessage = "";
            StateHasChanged();
        }
        catch (Exception ex)
        {
            saveMessage = $"Error al guardar: {ex.Message}";
            NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Error, Summary = "Error al Guardar", Detail = ex.Message });
        }
    }
}
