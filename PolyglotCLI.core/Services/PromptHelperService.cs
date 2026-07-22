using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public static class PromptHelperService
    {
        private static LlmProvider ParseProviderByUrl(string apiUrl)
        {
            string url = apiUrl.ToLowerInvariant();
            if (url.Contains("minimax")) return LlmProvider.MiniMax;
            if (url.Contains("moonshot") || url.Contains("kimi")) return LlmProvider.Kimi;
            if (url.Contains("dashscope") || url.Contains("qwen")) return LlmProvider.Qwen;
            if (url.Contains("openai")) return LlmProvider.OpenAi;
            if (url.Contains("anthropic")) return LlmProvider.Anthropic;
            if (url.Contains("generativelanguage") || url.Contains("gemini")) return LlmProvider.Gemini;
            if (url.Contains("ollama")) return LlmProvider.Ollama;
            if (url.Contains("1234")) return LlmProvider.LmStudio;
            if (url.Contains("8080")) return LlmProvider.LlamaCpp;
            return LlmProvider.Custom;
        }

        public static async Task<string> ImprovePromptAsync(string rawInput, string apiUrl, string model, int timeoutSeconds, double temperature)
        {
            string promptTemplate = "";
            try
            {
                var loader = new PromptLoader();
                promptTemplate = loader.LoadPromptImproverPrompt();
            }
            catch (Exception)
            {
                promptTemplate = "You are an expert translation prompt engineer. Rewrite the following translation instructions to be highly effective for an LLM. Output only the improved text, no intro, no markdown codeblocks:\n\n{user_input}";
            }

            string systemMessage = promptTemplate.Replace("{user_input}", rawInput);

            var config = AppConfig.Load();
            var provider = ParseProviderByUrl(apiUrl);
            string? apiKey = config.GetApiKeyForProvider(provider.ToString());

            using var client = LlmClientFactory.CreateClient(provider, apiUrl, apiKey, timeoutSeconds);
            client.Temperature = temperature;

            return await client.SendTextRequestAsync("You are an expert translation system prompt engineer.", systemMessage, model);
        }

        public static async Task<string> GenerateContextPromptAsync(
            string filePath, 
            string mode, 
            string pageRange, 
            AppConfig config, 
            string apiUrl, 
            string model, 
            string visionModel,
            Action<string> updateStatus)
        {
            string ocrPrompt = "You are a precise OCR system. Output the text exactly as it is shown in the image.";
            try
            {
                var loader = new PromptLoader();
                ocrPrompt = loader.LoadOcrPrompt();
            }
            catch {}

            using var client = LlmClientFactory.CreateClient(LlmProviderHelper.ParseProvider(config.Provider), apiUrl, config.ApiKey, config.TranslationTimeoutSeconds);
            var ocrService = new OcrService(client, ocrPrompt, visionModel);
            var pageRenderer = new PdfPageRenderer();

            var target = new DocumentTarget
            {
                FilePath = filePath,
                Mode = mode,
                PageRange = pageRange,
                MaxCharactersPerChunk = config.MaxCharactersPerChunk,
                ChunkOverlapCharacters = config.ChunkOverlapCharacters
            };

            updateStatus("Extracting text content from file...");

            var extractor = new DocumentExtractorFactory().GetExtractor(filePath);
            var pageStates = await extractor.ExtractTextAsync(filePath, target, ocrService, pageRenderer);

            var sb = new StringBuilder();
            foreach (var s in pageStates)
            {
                if (!string.IsNullOrEmpty(s.OcrText))
                {
                    sb.AppendLine(s.OcrText);
                }
            }
            string fileText = sb.ToString().Trim();

            if (string.IsNullOrEmpty(fileText))
            {
                throw new Exception("The file content could not be read or is empty.");
            }

            updateStatus("Analyzing context and generating prompt...");

            string systemPrompt = "You are an expert translation system prompt engineer. Analyze the following document text content to understand its context, main topic, style, tone, and specific domain terminology. " +
                                  "Based on this analysis, generate an optimal, detailed set of instructions (in English) that a translation model should follow when translating this document. The instructions should specify: " +
                                  "1) The context and topic of the text. " +
                                  "2) The tone, style, and formatting that must be preserved. " +
                                  "3) Any key domain-specific terminology or guidelines. " +
                                  "Output ONLY the generated instructions/additional prompt. Do NOT include any introductory or concluding text, and do NOT use markdown code blocks.";

            if (fileText.Length > 60000)
            {
                fileText = fileText.Substring(0, 60000) + "\n\n[TRUNCATED DUE TO SIZE]";
            }

            string userMessage = $"Here is the text content of the document to analyze:\n\n{fileText}";

            using var translationClient = LlmClientFactory.CreateClient(LlmProviderHelper.ParseProvider(config.Provider), apiUrl, config.ApiKey, config.PromptImproveTimeoutSeconds);
            translationClient.Temperature = config.Temperature;

            return await translationClient.SendTextRequestAsync(systemPrompt, userMessage, model);
        }

        public static async Task<string> AnalyzeErrorsAsync(string errorReport, string apiUrl, string model, int timeoutSeconds, double temperature)
        {
            string promptTemplate = "";
            try 
            {
                var loader = new PromptLoader();
                promptTemplate = loader.LoadErrorAnalysisPrompt();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load error analysis prompt: {ex.Message}");
            }

            var config = AppConfig.Load();
            var provider = ParseProviderByUrl(apiUrl);
            string? apiKey = config.GetApiKeyForProvider(provider.ToString());

            using var client = LlmClientFactory.CreateClient(provider, apiUrl, apiKey, timeoutSeconds);
            client.Temperature = temperature;

            return await client.SendTextRequestAsync(promptTemplate, errorReport, model);
        }
    }
}
