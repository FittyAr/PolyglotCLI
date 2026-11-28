using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PolyglotCLI.Localization;

namespace PolyglotCLI
{
    public static class PromptHelperService
    {
        /// <summary>
        /// Resuelve qué proveedor configurado corresponde a
        /// <paramref name="apiUrl"/> y qué key usar. La key SÓLO se envía
        /// si la URL coincide (mismo esquema, host y puerto) con la URL
        /// configurada para ese proveedor; una URL desconocida se trata
        /// como proveedor Custom SIN key, para no filtrar credenciales a
        /// un host arbitrario.
        /// </summary>
        internal static (LlmProvider Provider, string? ApiKey) ResolveProviderForUrl(AppConfig config, string apiUrl)
        {
            if (!Uri.TryCreate(apiUrl?.Trim(), UriKind.Absolute, out var target))
            {
                return (LlmProvider.Custom, null);
            }

            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(config.Provider)) candidates.Add(config.Provider);
            foreach (var name in new[] { config.TranslationProvider, config.OcrProvider, config.ReviewProvider })
            {
                if (!string.IsNullOrWhiteSpace(name)) candidates.Add(name);
            }
            candidates.AddRange(config.ProviderConfigs.Keys);

            foreach (var providerName in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var pCfg = config.GetProviderConfig(providerName);
                if (Uri.TryCreate(pCfg.ApiUrl?.Trim(), UriKind.Absolute, out var configured) && SameOrigin(configured, target))
                {
                    string? key = !string.IsNullOrWhiteSpace(pCfg.ApiKey)
                        ? pCfg.ApiKey
                        : (config.ProviderApiKeys.TryGetValue(providerName, out var k) ? k : null);
                    if (providerName.Equals(config.Provider, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(key))
                    {
                        key = config.ApiKey;
                    }
                    return (LlmProviderHelper.ParseProvider(providerName), key);
                }
            }

            AppLogger.Warn($"PromptHelperService: la URL '{apiUrl}' no coincide con ningún proveedor configurado; se usará sin API key.");
            return (LlmProvider.Custom, null);
        }

        private static bool SameOrigin(Uri a, Uri b)
        {
            return string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
                && a.Port == b.Port;
        }

        public static async Task<string> ImprovePromptAsync(string rawInput, string apiUrl, string model, int timeoutSeconds, double temperature, CancellationToken cancellationToken = default)
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
            var (provider, apiKey) = ResolveProviderForUrl(config, apiUrl);

            using var client = LlmClientFactory.CreateClient(provider, apiUrl, apiKey, timeoutSeconds);
            client.Temperature = temperature;

            return await client.SendTextRequestAsync("You are an expert translation system prompt engineer.", systemMessage, model, cancellationToken);
        }

        public static async Task<string> GenerateContextPromptAsync(
            string filePath, 
            string mode, 
            string pageRange, 
            AppConfig config, 
            string apiUrl, 
            string model, 
            string visionModel,
            Action<string> updateStatus,
            CancellationToken cancellationToken = default)
        {
            string ocrPrompt = "You are a precise OCR system. Output the text exactly as it is shown in the image.";
            try
            {
                var loader = new PromptLoader();
                ocrPrompt = loader.LoadOcrPrompt();
            }
            catch {}

            var (provider, apiKey) = ResolveProviderForUrl(config, apiUrl);
            using var client = LlmClientFactory.CreateClient(provider, apiUrl, apiKey, config.TranslationTimeoutSeconds);
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

            updateStatus(CoreText.Get("Core.ExtractingFileText", "Extracting text from the file..."));

            var extractor = new DocumentExtractorFactory().GetExtractor(filePath);
            // Contexto aislado: sin carpeta de job ni callbacks, así este
            // análisis nunca interfiere con un trabajo en curso.
            var context = new ExtractionContext { CancellationToken = cancellationToken };
            var pageStates = await extractor.ExtractTextAsync(filePath, target, ocrService, pageRenderer, context);

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
                throw new Exception(CoreText.Get("Core.FileContentEmpty", "The file content could not be read or is empty."));
            }

            updateStatus(CoreText.Get("Core.GeneratingContextPrompt", "Analyzing the context and generating the prompt..."));

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

            using var translationClient = LlmClientFactory.CreateClient(provider, apiUrl, apiKey, config.PromptImproveTimeoutSeconds);
            translationClient.Temperature = config.Temperature;

            return await translationClient.SendTextRequestAsync(systemPrompt, userMessage, model, cancellationToken);
        }

        public static async Task<string> AnalyzeErrorsAsync(string errorReport, string apiUrl, string model, int timeoutSeconds, double temperature, CancellationToken cancellationToken = default)
        {
            string promptTemplate = "";
            try 
            {
                var loader = new PromptLoader();
                promptTemplate = loader.LoadErrorAnalysisPrompt();
            }
            catch (Exception ex)
            {
                throw new Exception(CoreText.Format("Core.ErrorAnalysisPromptLoadFailed", "Failed to load the error analysis prompt: {0}", ex.Message));
            }

            var config = AppConfig.Load();
            var (provider, apiKey) = ResolveProviderForUrl(config, apiUrl);

            using var client = LlmClientFactory.CreateClient(provider, apiUrl, apiKey, timeoutSeconds);
            client.Temperature = temperature;

            return await client.SendTextRequestAsync(promptTemplate, errorReport, model, cancellationToken);
        }
    }
}
