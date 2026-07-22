using System;
using System.Collections.Generic;
using System.IO;

namespace PolyglotCLI
{
    public class CommandLineOptions
    {
        public List<string> Files { get; set; } = new List<string>();
        public List<DocumentTarget> DocumentTargets { get; set; } = new List<DocumentTarget>();
        public string Mode { get; set; } = "text"; // "text" or "image"
        public string Provider { get; set; } = "LmStudio";
        public string? OcrProvider { get; set; }
        public string? TranslationProvider { get; set; }
        public string? ReviewProvider { get; set; }
        public string ApiUrl { get; set; } = "http://172.22.144.1:1234/v1";
        public string? ApiKey { get; set; }
        public string? ModelName { get; set; }
        public string? VisionModelName { get; set; }
        public string TargetLanguage { get; set; } = "Spanish";
        public string OutputDirectory { get; set; } = "output";
        public string PageRange { get; set; } = "all";
        public bool Debug { get; set; } = false;
        public string? AdditionalPrompt { get; set; }

        public bool Transcribe { get; set; } = true;
        public bool Translate { get; set; } = true;
        public bool Verify { get; set; } = false;
        public bool GenerateDoc { get; set; } = false;
        public string? SelectedFormat { get; set; }
        public string? ResumeJobId { get; set; }

        public static CommandLineOptions? Parse(string[] args, AppConfig config)
        {
            var options = new CommandLineOptions
            {
                Provider = config.Provider,
                OcrProvider = config.OcrProvider,
                TranslationProvider = config.TranslationProvider,
                ReviewProvider = config.ReviewProvider,
                ApiUrl = config.ApiUrl,
                ApiKey = config.ApiKey,
                ModelName = config.DefaultModel,
                VisionModelName = config.DefaultVisionModel,
                AdditionalPrompt = config.AdditionalPrompt,
                Verify = config.EnableReview,
                GenerateDoc = !string.IsNullOrEmpty(config.DefaultOutputFormat),
                SelectedFormat = config.DefaultOutputFormat
            };
            var filesList = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-f":
                    case "--files":
                        while (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            filesList.Add(args[++i]);
                        }
                        break;
                    case "-m":
                    case "--mode":
                        if (i + 1 < args.Length)
                        {
                            options.Mode = args[++i].ToLowerInvariant();
                        }
                        break;
                    case "-a":
                    case "--api":
                        if (i + 1 < args.Length)
                        {
                            options.ApiUrl = args[++i];
                        }
                        break;
                    case "--provider":
                        if (i + 1 < args.Length)
                        {
                            options.Provider = args[++i];
                        }
                        break;
                    case "--ocr-provider":
                        if (i + 1 < args.Length)
                        {
                            options.OcrProvider = args[++i];
                        }
                        break;
                    case "--translation-provider":
                        if (i + 1 < args.Length)
                        {
                            options.TranslationProvider = args[++i];
                        }
                        break;
                    case "--review-provider":
                        if (i + 1 < args.Length)
                        {
                            options.ReviewProvider = args[++i];
                        }
                        break;
                    case "--api-key":
                    case "-key":
                        if (i + 1 < args.Length)
                        {
                            options.ApiKey = args[++i];
                        }
                        break;
                    case "--model":
                        if (i + 1 < args.Length)
                        {
                            options.ModelName = args[++i];
                        }
                        break;
                    case "-vmodel":
                    case "--vision-model":
                        if (i + 1 < args.Length)
                        {
                            options.VisionModelName = args[++i];
                        }
                        break;
                    case "-t":
                    case "--target-lang":
                        if (i + 1 < args.Length)
                        {
                            options.TargetLanguage = args[++i];
                        }
                        break;
                    case "-o":
                    case "--output-dir":
                        if (i + 1 < args.Length)
                        {
                            options.OutputDirectory = args[++i];
                        }
                        break;
                    case "-p":
                    case "--pages":
                        if (i + 1 < args.Length)
                        {
                            options.PageRange = args[++i];
                        }
                        break;
                    case "-ap":
                    case "--add-prompt":
                        if (i + 1 < args.Length)
                        {
                            options.AdditionalPrompt = args[++i];
                        }
                        break;
                    case "-d":
                    case "--debug":
                        options.Debug = true;
                        break;
                    case "--no-transcribe":
                        options.Transcribe = false;
                        break;
                    case "--no-translate":
                        options.Translate = false;
                        break;
                    case "--verify":
                        options.Verify = true;
                        break;
                    case "--generate-doc":
                        if (i + 1 < args.Length)
                        {
                            options.GenerateDoc = true;
                            options.SelectedFormat = args[++i].ToLowerInvariant();
                        }
                        break;
                    case "--resume-job":
                        if (i + 1 < args.Length)
                        {
                            options.ResumeJobId = args[++i];
                        }
                        break;
                    default:
                        // Treat unflagged arguments as input files if filesList is empty
                        if (!args[i].StartsWith("-"))
                        {
                            filesList.Add(args[i]);
                        }
                        break;
                }
            }

            options.Files = filesList;

            // Populate DocumentTargets list for backward compatibility with command line arguments
            options.DocumentTargets = new List<DocumentTarget>();
            foreach (var file in options.Files)
            {
                options.DocumentTargets.Add(new DocumentTarget
                {
                    FilePath = file,
                    Mode = options.Mode,
                    PageRange = options.PageRange
                });
            }

            if (options.Validate())
            {
                return options;
            }

            return null;
        }

        private bool Validate()
        {
            if (!string.IsNullOrEmpty(ResumeJobId))
            {
                return true; // El orquestador cargará y validará la configuración desde el manifiesto
            }

            if (Files.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: No PDF files specified. Use --files / -f to pass PDF files.");
                Console.ResetColor();
                PrintUsage();
                return false;
            }

            foreach (var file in Files)
            {
                if (!File.Exists(file))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: Input file '{file}' does not exist.");
                    Console.ResetColor();
                    return false;
                }
            }

            if (Mode != "text" && Mode != "image")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Invalid mode '{Mode}'. Supported modes are 'text' or 'image'.");
                Console.ResetColor();
                return false;
            }

            if (string.IsNullOrWhiteSpace(ApiUrl) || !Uri.TryCreate(ApiUrl, UriKind.Absolute, out _))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: Invalid LM Studio API URL '{ApiUrl}'.");
                Console.ResetColor();
                return false;
            }

            return true;
        }

        public static HashSet<int>? ParsePageRange(string rangeStr, int totalPages)
        {
            if (string.IsNullOrWhiteSpace(rangeStr) || rangeStr.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return null; // Null means all pages
            }

            var pages = new HashSet<int>();
            string[] parts = rangeStr.Split(',');
            foreach (var part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Contains('-'))
                {
                    string[] rangeParts = trimmed.Split('-');
                    if (rangeParts.Length == 2 && 
                        int.TryParse(rangeParts[0], out int start) && 
                        int.TryParse(rangeParts[1], out int end))
                    {
                        int min = Math.Min(start, end);
                        int max = Math.Max(start, end);
                        for (int i = min; i <= max; i++)
                        {
                            if (i >= 1 && i <= totalPages)
                            {
                                pages.Add(i);
                            }
                        }
                    }
                }
                else if (int.TryParse(trimmed, out int pageNum))
                {
                    if (pageNum >= 1 && pageNum <= totalPages)
                    {
                        pages.Add(pageNum);
                    }
                }
            }

            return pages;
        }

        public static bool IsValidPageRange(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return true;
            string trimmed = input.Trim().ToLowerInvariant();
            if (trimmed == "all") return true;

            // Permitir únicamente dígitos, comas, guiones y espacios
            foreach (char c in trimmed)
            {
                if (!char.IsDigit(c) && c != ',' && c != '-' && c != ' ')
                {
                    return false;
                }
            }

            // Separar por comas y validar cada fragmento
            string[] parts = trimmed.Split(',');
            foreach (var part in parts)
            {
                string p = part.Trim();
                if (string.IsNullOrEmpty(p)) return false;

                if (p.Contains('-'))
                {
                    string[] rangeParts = p.Split('-');
                    if (rangeParts.Length != 2) return false;
                    if (!int.TryParse(rangeParts[0].Trim(), out int start) || 
                        !int.TryParse(rangeParts[1].Trim(), out int end))
                    {
                        return false;
                    }
                    if (start <= 0 || end <= 0) return false;
                }
                else
                {
                    if (!int.TryParse(p, out int val) || val <= 0) return false;
                }
            }

            return true;
        }

        public static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run -- --files <file1.pdf> <file2.pdf> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -f, --files <paths>        Path(s) to the PDF file(s) to translate.");
            Console.WriteLine("  -m, --mode <text|image>    PDF content type. 'text' (extract text) or 'image' (OCR first). Default: text.");
            Console.WriteLine("  -a, --api <url>            LM Studio Base API URL. Default: http://172.22.144.1:1234/v1");
            Console.WriteLine("  --model <name>             Name of the translation LLM loaded in LM Studio.");
            Console.WriteLine("  -vmodel, --vision-model <name> Name of the vision model for OCR loaded in LM Studio.");
            Console.WriteLine("  -t, --target-lang <lang>   Target language for translation. Default: Spanish.");
            Console.WriteLine("  -o, --output-dir <path>    Directory where translated markdown files will be saved. Default: output.");
            Console.WriteLine("  -p, --pages <range>        Page range to process (e.g. '1-5', '12', '1,3,5'). Default: all.");
            Console.WriteLine("  -d, --debug                Debug mode. Only processes first 2 pages. Default: false.");
            Console.WriteLine("  -ap, --add-prompt <prompt>  Additional translation prompt guidance.");
        }
    }

    public class DocumentTarget
    {
        public string FilePath { get; set; } = string.Empty;
        public string Mode { get; set; } = "text"; // "text" or "image" (specific to PDF)
        public string PageRange { get; set; } = "all";
        public int MaxCharactersPerChunk { get; set; } = 6000;
        public int ChunkOverlapCharacters { get; set; } = 300;
    }
}
