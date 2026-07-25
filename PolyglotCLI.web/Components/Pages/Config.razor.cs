using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Radzen;
using PolyglotCLI;
using PolyglotCLI.web.Components.Config;
using PolyglotCLI.web.Components.Dialogs;

namespace PolyglotCLI.web.Components.Pages;

public partial class Config : ComponentBase, IDisposable
{
    [Inject]
    protected PolyglotCLI.AppConfig AppConfig { get; set; } = default!;

    [Inject]
    protected NotificationService NotificationService { get; set; } = default!;

    [Inject]
    protected DialogService DialogService { get; set; } = default!;

    [Inject]
    protected NavigationManager NavigationManager { get; set; } = default!;

    protected string saveMessage = "";
    protected string outputFormatsInput = "";
    protected string inputExtensionsInput = "";
    protected List<string> availableModels = new List<string>();
    protected List<string> outputFormatOptions = new List<string> { "html", "docx", "odf", "pdf" };
    protected bool isTestingConnection = false;
    protected string? testConnectionResult = null;

    // Referencia a la pestaña "General" para poder leer/escribir
    // cambios que viven solo en el componente hijo (caso típico: la
    // nueva API Key escrita en su input, que no se aplica a
    // AppConfig hasta el Save). Sin este @ref no podríamos saber
    // que hay cambios pendientes ni aplicarlos.
    private GeneralConfigTab? generalTabRef;

    // Prompts files content
    protected string ocrPromptText = "";
    protected string translationPromptText = "";
    protected string reviewPromptText = "";
    protected string promptImproverPromptText = "";

    // Snapshots del texto de prompts al momento del load. Sirven
    // para detectar dirty en los prompts (IsDirty solo chequeaba
    // el JSON de AppConfig — los prompts son un recurso aparte
    // y se guardan en archivos .md via PromptLoader.Save*).
    private string _ocrPromptBaseline = "";
    private string _translationPromptBaseline = "";
    private string _reviewPromptBaseline = "";
    private string _promptImproverPromptBaseline = "";

    // ── Dirty tracking ────────────────────────────────────────────────
    // Serializamos el AppConfig al entrar a la página y cada vez que
    // guardamos. La navegación SPA sale interceptada por el
    // locationHandler si hay cambios pendientes.
    private string? _baselineJson;
    private IDisposable? _locationHandler;
    private bool _handlerRegistered;

    protected override async Task OnInitializedAsync()
    {
        AppConfig.Reload();
        outputFormatsInput = string.Join(", ", AppConfig.SupportedOutputFormats);
        inputExtensionsInput = string.Join(", ", AppConfig.SupportedInputExtensions);
        outputFormatOptions = AppConfig.SupportedOutputFormats ?? new List<string> { "html", "docx", "odf", "pdf" };

        // Load prompt files using PromptLoader
        try
        {
            var promptLoader = new PromptLoader();
            try { ocrPromptText = promptLoader.LoadOcrPrompt(); } catch {}
            try { translationPromptText = promptLoader.LoadTranslationPrompt(); } catch {}
            try { reviewPromptText = promptLoader.LoadReviewPrompt(); } catch {}
            try { promptImproverPromptText = promptLoader.LoadPromptImproverPrompt(); } catch {}
        }
        catch {}

        // Snapshots para detectar cambios en prompts (los prompts
        // viven en archivos .md, no en el AppConfig JSON, así que
        // el baseline JSON no los cubre).
        _ocrPromptBaseline = ocrPromptText;
        _translationPromptBaseline = translationPromptText;
        _reviewPromptBaseline = reviewPromptText;
        _promptImproverPromptBaseline = promptImproverPromptText;

        // Fetch models dynamically from LLM Provider
        try {
            using var client = LlmClientFactory.CreateClient(AppConfig, 3);
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

        // Capturamos el baseline después de Reload + carga inicial.
        // Cualquier cambio posterior se detecta comparando contra este JSON.
        _baselineJson = SerializeForCompare(AppConfig);
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && !_handlerRegistered)
        {
            _locationHandler = NavigationManager.RegisterLocationChangingHandler(OnLocationChanging);
            _handlerRegistered = true;
        }
    }

    /// <summary>
    /// Intercepta la navegación SPA. Si hay cambios sin guardar,
    /// muestra el modal y decide según la respuesta del usuario.
    /// </summary>
    private async ValueTask OnLocationChanging(LocationChangingContext context)
    {
        if (!IsDirty())
            return; // Sin cambios: dejar pasar.

        // El target siempre es distinto de la URL actual (RegisterLocationChangingHandler
        // solo se llama cuando hay cambio real de ubicación).
        var result = await DialogService.OpenAsync<UnsavedChangesDialog>(
            title: "Cambios sin guardar",
            options: new DialogOptions
            {
                Width = "480px",
                CloseDialogOnEsc = true,
                CloseDialogOnOverlayClick = false
            });

        if (result is UnsavedChangesDialog.Choice choice)
        {
            switch (choice)
            {
                case UnsavedChangesDialog.Choice.Cancel:
                    context.PreventNavigation();
                    break;

                case UnsavedChangesDialog.Choice.Discard:
                    ApplyBaseline();
                    // No llamamos PreventNavigation: dejamos pasar con el
                    // estado descartado.
                    break;

                case UnsavedChangesDialog.Choice.Save:
                    await SaveConfig(AppConfig);
                    // Si SaveConfig reportó error, el baseline no se
                    // actualizó → todavía dirty → bloqueamos para que
                    // el usuario no pierda los cambios.
                    if (IsDirty())
                        context.PreventNavigation();
                    break;
            }
        }
        else
        {
            // Cerrado por Esc u overlay (aunque overlay está bloqueado).
            // Interpretamos como "cancelar" para no perder datos.
            context.PreventNavigation();
        }
    }

    private bool IsDirty()
    {
        if (_baselineJson is null)
            return false;
        var current = SerializeForCompare(AppConfig);
        if (!string.Equals(current, _baselineJson, StringComparison.Ordinal))
            return true;
        // Aunque los campos persistidos no hayan cambiado, el input
        // de "nueva API Key" en GeneralConfigTab puede tener algo
        // pendiente. Esos cambios viven en el componente hijo y no
        // aparecen en el AppConfig hasta ApplyToConfig().
        if (generalTabRef?.HasPendingNewKey == true)
            return true;
        // Los prompts viven en archivos .md aparte (no en el JSON
        // de AppConfig), así que también hay que chequearlos contra
        // sus baselines. Si el user edita un prompt y navega away
        // sin guardar, debe dispararse el modal de "cambios sin
        // guardar".
        if (ocrPromptText != _ocrPromptBaseline) return true;
        if (translationPromptText != _translationPromptBaseline) return true;
        if (reviewPromptText != _reviewPromptBaseline) return true;
        if (promptImproverPromptText != _promptImproverPromptBaseline) return true;
        return false;
    }

    private void ApplyBaseline()
    {
        if (_baselineJson is null)
            return;
        try
        {
            // Restaurar todos los campos del snapshot al AppConfig vivo.
            // Es la forma más simple de "deshacer": reemplazamos por el
            // estado original serializado. No toca LoadedFromPath.
            var snapshot = JsonSerializer.Deserialize<PolyglotCLI.AppConfig>(_baselineJson);
            if (snapshot is null) return;
            CopyInto(snapshot, AppConfig);
            // También revertimos los buffers locales de los prompts:
            try
            {
                var promptLoader = new PromptLoader();
                try { ocrPromptText = promptLoader.LoadOcrPrompt(); } catch {}
                try { translationPromptText = promptLoader.LoadTranslationPrompt(); } catch {}
                try { reviewPromptText = promptLoader.LoadReviewPrompt(); } catch {}
                try { promptImproverPromptText = promptLoader.LoadPromptImproverPrompt(); } catch {}
            }
            catch { }
        }
        catch
        {
            // Si falla el deserializar, no hacemos nada: el estado queda
            // como está y el usuario verá los cambios (peor escenario
            // aceptable).
        }
        // También revertimos los buffers locales de los prompts:
        try
        {
            var promptLoader = new PromptLoader();
            try { ocrPromptText = promptLoader.LoadOcrPrompt(); } catch {}
            try { translationPromptText = promptLoader.LoadTranslationPrompt(); } catch {}
            try { reviewPromptText = promptLoader.LoadReviewPrompt(); } catch {}
            try { promptImproverPromptText = promptLoader.LoadPromptImproverPrompt(); } catch {}
        }
        catch { }
        // Reset baselines para que IsDirty no reporte los textos
        // recién recargados como cambios pendientes.
        _ocrPromptBaseline = ocrPromptText;
        _translationPromptBaseline = translationPromptText;
        _reviewPromptBaseline = reviewPromptText;
        _promptImproverPromptBaseline = promptImproverPromptText;
        // Descartar también cualquier cambio pendiente que viviera en
        // componentes hijos (ej: la nueva API Key tipeada en
        // GeneralConfigTab pero todavía no aplicada a AppConfig).
        generalTabRef?.DiscardPendingChange();
    }

    private static string SerializeForCompare(PolyglotCLI.AppConfig cfg)
    {
        // Serializamos solo los campos persistibles. LoadedFromPath es
        // [JsonIgnore] así que ya queda fuera; igual lo limpiamos
        // explícitamente para que el snapshot sea estable entre
        // re-renders.
        var copy = new PolyglotCLI.AppConfig();
        CopyInto(cfg, copy);
        copy.LoadedFromPath = null;
        return JsonSerializer.Serialize(copy);
    }

    private static void CopyInto(PolyglotCLI.AppConfig src, PolyglotCLI.AppConfig dst)
    {
        dst.Provider = src.Provider;
        dst.OcrProvider = src.OcrProvider;
        dst.TranslationProvider = src.TranslationProvider;
        dst.ReviewProvider = src.ReviewProvider;
        dst.ApiUrl = src.ApiUrl;
        dst.ApiKey = src.ApiKey;
        dst.ProviderApiKeys = new Dictionary<string, string>(src.ProviderApiKeys);
        dst.ProviderConfigs = new Dictionary<string, ProviderConfig>(src.ProviderConfigs);
        dst.DefaultModel = src.DefaultModel;
        dst.DefaultVisionModel = src.DefaultVisionModel;
        dst.TargetLanguage = src.TargetLanguage;
        dst.OutputDirectory = src.OutputDirectory;
        dst.LastScanDirectory = src.LastScanDirectory;
        dst.Debug = src.Debug;
        dst.AdditionalPrompt = src.AdditionalPrompt;
        dst.TranslationTimeoutSeconds = src.TranslationTimeoutSeconds;
        dst.PromptImproveTimeoutSeconds = src.PromptImproveTimeoutSeconds;
        dst.ModelCheckTimeoutSeconds = src.ModelCheckTimeoutSeconds;
        dst.Temperature = src.Temperature;
        dst.MaxCharactersPerChunk = src.MaxCharactersPerChunk;
        dst.ChunkOverlapCharacters = src.ChunkOverlapCharacters;
        dst.PreserveFormat = src.PreserveFormat;
        dst.EnableReview = src.EnableReview;
        dst.ReviewModel = src.ReviewModel;
        dst.ReviewTimeoutSeconds = src.ReviewTimeoutSeconds;
        dst.ReviewTemperature = src.ReviewTemperature;
        dst.OcrTemperature = src.OcrTemperature;
        dst.OcrTimeoutSeconds = src.OcrTimeoutSeconds;
        dst.OutputFormats = src.OutputFormats;
        dst.SaveMarkdown = src.SaveMarkdown;
        dst.ModuleExtractionEnabled = src.ModuleExtractionEnabled;
        dst.ModuleTranslationEnabled = src.ModuleTranslationEnabled;
        dst.ModuleReviewEnabled = src.ModuleReviewEnabled;
        dst.ModuleConversionEnabled = src.ModuleConversionEnabled;
        dst.DefaultOutputFormat = src.DefaultOutputFormat;
        dst.SupportedOutputFormats = new List<string>(src.SupportedOutputFormats);
        dst.SupportedInputExtensions = new List<string>(src.SupportedInputExtensions);
        dst.LogDirectory = src.LogDirectory;
        dst.LogLevelConsole = src.LogLevelConsole;
        dst.LogLevelFile = src.LogLevelFile;
    }

    public void Dispose()
    {
        _locationHandler?.Dispose();
        _locationHandler = null;
        _handlerRegistered = false;
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
            // Si el usuario tipeó una nueva API key en el input de
            // GeneralConfigTab pero todavía no guardó, está en el
            // buffer `newApiKey` (no en AppConfig). Sin aplicar el
            // cambio acá, el test usaría la key vieja y el
            // SaveTestedProvider persistiría esa misma key vieja —
            // perdiendo el cambio del usuario si cierra la página
            // sin Guardar.
            if (generalTabRef?.HasPendingNewKey == true)
            {
                generalTabRef.ApplyToConfig();
            }

            using var client = LlmClientFactory.CreateClient(AppConfig, AppConfig.ModelCheckTimeoutSeconds);
            availableModels = await client.GetAvailableModelsAsync();
            AppConfig.SaveTestedProvider(AppConfig.Provider, AppConfig.ApiUrl, AppConfig.ApiKey, availableModels);
            // SaveTestedProvider muta el config → refrescamos el baseline.
            _baselineJson = SerializeForCompare(AppConfig);
            NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Success, Summary = "Carga y Registro Exitoso", Detail = $"Servidor '{AppConfig.Provider}' verificado con {availableModels.Count} modelos y registrado para usar en OCR, Traducción y Revisión." });
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
            // Si el usuario escribió una nueva API Key en el input
            // de GeneralConfigTab, todavía no está en AppConfig (para
            // no exponer la key anterior en claro). La aplicamos acá,
            // justo antes de Save, que a su vez la cifra a DPAPI.
            generalTabRef?.ApplyToConfig();

            args.SupportedOutputFormats = outputFormatsInput
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            args.SupportedInputExtensions = inputExtensionsInput
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            args.Save();

            // Save system prompt files using PromptLoader
            try
            {
                var promptLoader = new PromptLoader();
                promptLoader.SaveOcrPrompt(ocrPromptText ?? "");
                promptLoader.SaveTranslationPrompt(translationPromptText ?? "");
                promptLoader.SaveReviewPrompt(reviewPromptText ?? "");
                promptLoader.SavePromptImproverPrompt(promptImproverPromptText ?? "");
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Failed to save some prompt files: {ex.Message}");
            }

            // Reset baselines: lo que está en disco ahora coincide
            // con lo que está en memoria, así que no hay cambios
            // pendientes (incluyendo los prompts).
            _ocrPromptBaseline = ocrPromptText;
            _translationPromptBaseline = translationPromptText;
            _reviewPromptBaseline = reviewPromptText;
            _promptImproverPromptBaseline = promptImproverPromptText;

            saveMessage = "Configuración guardada correctamente!";
            NotificationService.Notify(new NotificationMessage { Severity = NotificationSeverity.Success, Summary = "Éxito", Detail = "Configuración guardada correctamente." });

            // Refrescar baseline: lo que está en el modelo YA está
            // persistido en disco, así que el nuevo estado limpio.
            _baselineJson = SerializeForCompare(AppConfig);

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
