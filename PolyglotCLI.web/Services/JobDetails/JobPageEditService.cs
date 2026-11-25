using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PolyglotCLI;

namespace PolyglotCLI.web.Services.JobDetails;

public interface IJobPageEditService
{
    List<PageProcessState> BuildPageProcessStates(
        IEnumerable<DocumentPageData> pageDataList,
        IReadOnlyDictionary<int, string> pendingTranslationEdits);

    List<DocumentPageData> BuildDocumentPageDataForExport(
        IEnumerable<DocumentPageData> pageDataList,
        IReadOnlyDictionary<int, string> pendingTranslationEdits);

    Task PersistTranslationEditAsync(
        List<DocumentPageData> pageDataList,
        IReadOnlyDictionary<int, string> pendingTranslationEdits,
        string jobDir,
        string jsonFileName,
        JobManifest job,
        JobFileManifest? fileManifest,
        AppConfig config);
}

public class JobPageEditService : IJobPageEditService
{
    public List<PageProcessState> BuildPageProcessStates(
        IEnumerable<DocumentPageData> pageDataList,
        IReadOnlyDictionary<int, string> pendingTranslationEdits)
    {
        return pageDataList.Select(d => new PageProcessState
        {
            PageNumber = d.PageNumber,
            OcrText = d.OriginalText,
            TranslatedText = pendingTranslationEdits.TryGetValue(d.PageNumber, out var edited) ? edited : d.TranslatedText,
            ReviewedText = d.ReviewedText,
            OcrFailed = !d.IsOcrSuccessful,
            TranslationFailed = !d.IsTranslationSuccessful,
            OcrErrorMessage = d.OcrErrorMessage,
            TranslationErrorMessage = d.TranslationErrorMessage,
            ThoughtText = d.ThoughtText
        }).ToList();
    }

    public List<DocumentPageData> BuildDocumentPageDataForExport(
        IEnumerable<DocumentPageData> pageDataList,
        IReadOnlyDictionary<int, string> pendingTranslationEdits)
    {
        return pageDataList.Select(d => new DocumentPageData
        {
            PageNumber = d.PageNumber,
            OriginalText = d.OriginalText,
            TranslatedText = pendingTranslationEdits.TryGetValue(d.PageNumber, out var edited) ? edited : d.TranslatedText,
            ReviewedText = d.ReviewedText,
            IsOcrSuccessful = d.IsOcrSuccessful,
            IsTranslationSuccessful = d.IsTranslationSuccessful,
            OcrErrorMessage = d.OcrErrorMessage,
            TranslationErrorMessage = d.TranslationErrorMessage,
            UsedTemperature = d.UsedTemperature,
            RetryCount = d.RetryCount,
            ThoughtText = d.ThoughtText
        }).ToList();
    }

    public async Task PersistTranslationEditAsync(
        List<DocumentPageData> pageDataList,
        IReadOnlyDictionary<int, string> pendingTranslationEdits,
        string jobDir,
        string jsonFileName,
        JobManifest job,
        JobFileManifest? fileManifest,
        AppConfig config)
    {
        string filePath = Path.Combine(jobDir, "data", jsonFileName);
        if (!File.Exists(filePath))
        {
            return;
        }

        var states = BuildPageProcessStates(pageDataList, pendingTranslationEdits);
        JobManifestService.SavePageStatesToJson(states, filePath);

        if (fileManifest == null)
        {
            return;
        }

        // Idioma y carpeta de salida del TRABAJO, no de la configuración
        // actual: si el usuario cambió el idioma destino después de
        // traducir, escribir con el idioma actual pisaría otro archivo y
        // dejaría desactualizado el original.
        string targetLanguage = !string.IsNullOrWhiteSpace(fileManifest.TargetLanguage)
            ? fileManifest.TargetLanguage
            : (!string.IsNullOrWhiteSpace(job.TargetLanguage) ? job.TargetLanguage : config.TargetLanguage);
        string outputDir = !string.IsNullOrWhiteSpace(job.OutputDirectory)
            ? new AppConfig { OutputDirectory = job.OutputDirectory }.AbsoluteOutputDirectory
            : config.AbsoluteOutputDirectory;
        Directory.CreateDirectory(outputDir);

        string sourceFileName = jsonFileName.Replace("_data.json", string.Empty, System.StringComparison.OrdinalIgnoreCase);
        string outputPath = Path.Combine(outputDir, $"{sourceFileName}_{targetLanguage}.md");

        var mergedForExport = BuildDocumentPageDataForExport(pageDataList, pendingTranslationEdits);
        MarkdownWriter.ExportToMarkdown(outputPath, sourceFileName, targetLanguage, mergedForExport, false);

        string outputsDir = Path.Combine(jobDir, "outputs");
        if (!Directory.Exists(outputsDir))
        {
            Directory.CreateDirectory(outputsDir);
        }
        File.Copy(outputPath, Path.Combine(outputsDir, Path.GetFileName(outputPath)), true);

        if (!string.IsNullOrEmpty(config.DefaultOutputFormat) && config.ModuleConversionEnabled)
        {
            await OutputFormatConverter.ConvertToFormatsAsync(outputPath, config.DefaultOutputFormat);
        }
    }
}
