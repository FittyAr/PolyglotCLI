using System;
using System.Text;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public static class ConsoleErrorAnalysisService
    {
        public static async Task PromptForErrorAnalysisAsync(JobManifest currentManifest, AppConfig config)
        {
            Console.WriteLine("\n[Error Analysis]");
            Console.WriteLine("One or more pages failed during the job.");
            Console.Write("Would you like to analyze these errors using AI to get recommendations? (y/N): ");
            string? ans = Console.ReadLine();
            if (ans != null && ans.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var sbErr = new StringBuilder();
                    foreach (var f in currentManifest.Files)
                    {
                        foreach (var p in f.Pages)
                        {
                            if (!p.OcrCompleted && !string.IsNullOrEmpty(p.OcrError))
                                sbErr.AppendLine($"File: {f.OriginalFileName}, Page: {p.PageNumber}, Phase: OCR, Error: {p.OcrError}");
                            if (!p.TranslationCompleted && !string.IsNullOrEmpty(p.TranslationError))
                                sbErr.AppendLine($"File: {f.OriginalFileName}, Page: {p.PageNumber}, Phase: Translation, Error: {p.TranslationError}");
                        }
                    }
                    
                    Console.WriteLine("Analyzing errors... please wait.");
                    string model = !string.IsNullOrEmpty(config.DefaultModel) ? config.DefaultModel : "unknown";
                    string recommendation = await PromptHelperService.AnalyzeErrorsAsync(
                        sbErr.ToString(), 
                        config.ApiUrl, 
                        model, 
                        config.TranslationTimeoutSeconds, 
                        config.Temperature
                    );
                    
                    Console.WriteLine("\n--- AI Recommendation ---");
                    Console.WriteLine(recommendation);
                    Console.WriteLine("-------------------------\n");
                    
                    Console.Write("Do you want to retry this job now? (y/N): ");
                    string? retryAns = Console.ReadLine();
                    if (retryAns != null && retryAns.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"To resume this job with new settings, edit the config or use the TUI Jobs History [F9].");
                        Console.WriteLine($"Job ID: {currentManifest.JobId}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during analysis: {ex.Message}");
                }
            }
        }
    }
}
