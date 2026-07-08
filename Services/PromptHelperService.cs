using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public static class PromptHelperService
    {
        public static async Task<string> ImprovePromptAsync(string rawInput, string apiUrl, string model, int timeoutSeconds, double temperature)
        {
            string promptTemplate = "";
            string promptPath = Path.Combine("prompts", "prompt_improver_prompt.md");
            if (File.Exists(promptPath))
            {
                promptTemplate = await File.ReadAllTextAsync(promptPath);
            }
            else
            {
                promptTemplate = "You are an expert translation prompt engineer. Rewrite the following translation instructions to be highly effective for an LLM. Output only the improved text, no intro, no markdown codeblocks:\n\n{user_input}";
            }

            string systemMessage = promptTemplate.Replace("{user_input}", rawInput);

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "user", content = systemMessage }
                },
                temperature = temperature
            };

            string jsonString = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{apiUrl.TrimEnd('/')}/chat/completions", content);
            if (response.IsSuccessStatusCode)
            {
                string responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    string text = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                    return text.Trim();
                }
            }
            else
            {
                string errContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API returned status code: {response.StatusCode}. Details: {errContent}");
            }

            throw new Exception("No output returned from AI.");
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

            using var client = new LmStudioClient(apiUrl, config.TranslationTimeoutSeconds);
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

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                temperature = config.Temperature
            };

            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(config.PromptImproveTimeoutSeconds);

            string jsonString = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{apiUrl.TrimEnd('/')}/chat/completions", content);
            if (response.IsSuccessStatusCode)
            {
                string responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    string text = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                    return text.Trim();
                }
            }
            else
            {
                string errContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API returned status code: {response.StatusCode}. Details: {errContent}");
            }

            throw new Exception("No output returned from AI.");
        }
    }
}
