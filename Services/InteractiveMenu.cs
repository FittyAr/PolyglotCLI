using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    public static class InteractiveMenu
    {
        public static async Task<CommandLineOptions?> RunAsync(AppConfig config)
        {
            Console.Clear();
            Console.WriteLine("==================================================");
            Console.WriteLine("        PDF Local Translator - Interactive Setup  ");
            Console.WriteLine("==================================================");
            Console.WriteLine("Leave field empty to use the default value [in brackets].");
            Console.WriteLine();

            var options = new CommandLineOptions();

            // 1. LM Studio API URL
            Console.Write($"Enter LM Studio API URL [{config.ApiUrl}]: ");
            string? apiUrlInput = Console.ReadLine();
            options.ApiUrl = string.IsNullOrWhiteSpace(apiUrlInput) ? config.ApiUrl : apiUrlInput.Trim();

            // 2. Fetch available models from LM Studio
            Console.WriteLine("\nConnecting to LM Studio to retrieve loaded models...");
            using var client = new LmStudioClient(options.ApiUrl);
            var loadedModels = new List<string>();
            try
            {
                var response = await client.GetFirstLoadedModelAsync(); // We can fetch models list
                // To fetch all models, let's make a call or reuse the client
                // For simplicity, let's query the API for models list
                using var httpClient = new System.Net.Http.HttpClient();
                var modelsResponse = await httpClient.GetAsync($"{options.ApiUrl.TrimEnd('/')}/models");
                if (modelsResponse.IsSuccessStatusCode)
                {
                    string content = await modelsResponse.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(content);
                    foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
                    {
                        string id = item.GetProperty("id").GetString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(id))
                        {
                            loadedModels.Add(id);
                        }
                    }
                }
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Warning: Could not connect to LM Studio to fetch loaded models. You will need to enter model names manually.");
                Console.ResetColor();
            }

            // 3. PDF Files
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("Enter path to PDF file(s).");
                Console.WriteLine("- For multiple files, separate with a comma (e.g. doc1.pdf, doc2.pdf)");
                Console.WriteLine("- You can drag & drop file(s) into this console window");
                Console.Write("PDF file path(s): ");
                string? filesInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(filesInput))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: You must specify at least one PDF file.");
                    Console.ResetColor();
                    continue;
                }

                var files = new List<string>();
                string[] parts = filesInput.Split(',');
                bool allExist = true;

                foreach (var part in parts)
                {
                    // Clean drag and drop quotes
                    string path = part.Trim().Trim('"', '\'');
                    if (string.IsNullOrEmpty(path)) continue;

                    if (!File.Exists(path))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error: File '{path}' does not exist.");
                        Console.ResetColor();
                        allExist = false;
                        break;
                    }
                    files.Add(path);
                }

                if (allExist && files.Count > 0)
                {
                    options.Files = files;
                    break;
                }
            }

            // 4. Mode Selection
            Console.WriteLine();
            Console.WriteLine("Select processing mode:");
            Console.WriteLine("  1. Text [Default] (Extract selectable text directly)");
            Console.WriteLine("  2. Image          (Render pages and perform OCR via Vision LLM)");
            Console.Write("Choice (1-2): ");
            string? modeInput = Console.ReadLine();
            options.Mode = (modeInput == "2") ? "image" : "text";

            // 5. Select Translation Model
            Console.WriteLine();
            string defaultTextModel = config.DefaultModel ?? (loadedModels.Count > 0 ? loadedModels[0] : "default-model");
            if (loadedModels.Count > 0)
            {
                Console.WriteLine("Available models in LM Studio:");
                for (int i = 0; i < loadedModels.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}. {loadedModels[i]}");
                }
                Console.WriteLine($"  {loadedModels.Count + 1}. Enter manually...");
                Console.Write($"Select Translation Model [Default: {defaultTextModel}]: ");
                string? modelChoiceInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(modelChoiceInput))
                {
                    options.ModelName = defaultTextModel;
                }
                else if (int.TryParse(modelChoiceInput, out int choice) && choice >= 1 && choice <= loadedModels.Count)
                {
                    options.ModelName = loadedModels[choice - 1];
                }
                else
                {
                    Console.Write("Enter model name: ");
                    options.ModelName = Console.ReadLine()?.Trim();
                }
            }
            else
            {
                Console.Write($"Enter Translation Model Name [{defaultTextModel}]: ");
                string? modelInput = Console.ReadLine();
                options.ModelName = string.IsNullOrWhiteSpace(modelInput) ? defaultTextModel : modelInput.Trim();
            }

            // 6. Select Vision Model (Only in image mode)
            if (options.Mode == "image")
            {
                Console.WriteLine();
                string defaultVisionModel = config.DefaultVisionModel ?? (loadedModels.Count > 0 ? loadedModels[0] : "default-model");
                if (loadedModels.Count > 0)
                {
                    Console.WriteLine("Available models in LM Studio:");
                    for (int i = 0; i < loadedModels.Count; i++)
                    {
                        Console.WriteLine($"  {i + 1}. {loadedModels[i]}");
                    }
                    Console.WriteLine($"  {loadedModels.Count + 1}. Enter manually...");
                    Console.Write($"Select Vision Model for OCR [Default: {defaultVisionModel}]: ");
                    string? visionModelChoiceInput = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(visionModelChoiceInput))
                    {
                        options.VisionModelName = defaultVisionModel;
                    }
                    else if (int.TryParse(visionModelChoiceInput, out int choice) && choice >= 1 && choice <= loadedModels.Count)
                    {
                        options.VisionModelName = loadedModels[choice - 1];
                    }
                    else
                    {
                        Console.Write("Enter vision model name: ");
                        options.VisionModelName = Console.ReadLine()?.Trim();
                    }
                }
                else
                {
                    Console.Write($"Enter Vision Model Name for OCR [{defaultVisionModel}]: ");
                    string? visionModelInput = Console.ReadLine();
                    options.VisionModelName = string.IsNullOrWhiteSpace(visionModelInput) ? defaultVisionModel : visionModelInput.Trim();
                }
            }

            // 7. Target Language
            Console.WriteLine();
            Console.Write("Enter target language [Spanish]: ");
            string? langInput = Console.ReadLine();
            options.TargetLanguage = string.IsNullOrWhiteSpace(langInput) ? "Spanish" : langInput.Trim();

            // 8. Output Directory
            Console.Write("Enter output directory [output]: ");
            string? outDirInput = Console.ReadLine();
            options.OutputDirectory = string.IsNullOrWhiteSpace(outDirInput) ? "output" : outDirInput.Trim();

            // 9. Page Range
            Console.WriteLine();
            Console.Write("Enter page range to process (e.g. '1-5', '12', '1,3,5' or 'all') [all]: ");
            string? pageRangeInput = Console.ReadLine();
            options.PageRange = string.IsNullOrWhiteSpace(pageRangeInput) ? "all" : pageRangeInput.Trim();

            // 10. Debug Mode
            Console.Write("Enable debug mode? (Only processes first 2 pages for speed) (y/N): ");
            string? debugInput = Console.ReadLine()?.Trim().ToLowerInvariant();
            options.Debug = (debugInput == "y" || debugInput == "yes");

            // Confirmation
            Console.WriteLine();
            Console.WriteLine("Configuration Complete!");
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine($"Files:            {string.Join(", ", options.Files)}");
            Console.WriteLine($"Mode:             {options.Mode.ToUpperInvariant()}");
            Console.WriteLine($"API URL:          {options.ApiUrl}");
            Console.WriteLine($"Text Model:       {options.ModelName}");
            if (options.Mode == "image")
            {
                Console.WriteLine($"Vision Model:     {options.VisionModelName}");
            }
            Console.WriteLine($"Target Language:  {options.TargetLanguage}");
            Console.WriteLine($"Output Directory: {options.OutputDirectory}");
            Console.WriteLine($"Page Range:       {options.PageRange}");
            Console.WriteLine($"Debug Mode:       {options.Debug}");
            Console.WriteLine("--------------------------------------------------");
            Console.Write("Proceed with translation? (Y/n): ");
            string? proceedChoice = Console.ReadLine();

            if (proceedChoice?.Trim().ToLowerInvariant() == "n")
            {
                Console.WriteLine("Translation cancelled.");
                return null;
            }

            return options;
        }
    }
}
