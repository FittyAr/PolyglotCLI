using System;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            AppLogger.Info("Program Main started.");
            
            Console.WriteLine("==================================================");
            Console.WriteLine("                  PolyglotCLI                     ");
            Console.WriteLine("==================================================");

            // Load Configuration
            AppLogger.Info("Loading config.json...");
            var config = AppConfig.Load();
            AppLogger.Initialize(config);
            AppLogger.Info($"Loaded configuration API URL: '{config.ApiUrl}'");

            // Test Conversion CLI Override
            if (args.Length > 0 && args[0] == "--test-conversion")
            {
                if (args.Length < 3)
                {
                    Console.WriteLine("Usage: --test-conversion <markdownPath> <formats>");
                    return 1;
                }
                string mdPath = args[1];
                string fmts = args[2];
                Console.WriteLine($"Running test conversion for: {mdPath} to: {fmts}");
                await OutputFormatConverter.ConvertToFormatsAsync(mdPath, fmts);
                return 0;
            }

            try
            {
                // 1. Parse command line options or launch interactive menu
                CommandLineOptions? options;
                if (args.Length == 0)
                {
                    AppLogger.Info("No command-line arguments specified. Launching interactive menu...");
                    options = await InteractiveMenu.RunAsync(config);
                    if (options == null)
                    {
                        AppLogger.Info("Interactive menu cancelled by user. Exiting.");
                        return 0; // Cancelled by user
                    }
                    AppLogger.Info("Interactive menu completed. Running translation task.");
                }
                else
                {
                    AppLogger.Info($"Parsing {args.Length} command-line arguments: {string.Join(" ", args)}");
                    options = CommandLineOptions.Parse(args, config);
                    if (options == null)
                    {
                        AppLogger.Error("CommandLineOptions parsing failed. Exiting.");
                        return 1;
                    }
                    AppLogger.Info("CommandLineOptions parsing succeeded.");
                }

                // 2. Delegate execution orchestration to TranslationOrchestrator
                int exitCode = await TranslationOrchestrator.ExecuteAsync(options, config);
                
                if (exitCode != 0 && TranslationOrchestrator.CurrentJobDirectory != null)
                {
                    string manifestPath = System.IO.Path.Combine(TranslationOrchestrator.CurrentJobDirectory, "manifest.json");
                    if (System.IO.File.Exists(manifestPath))
                    {
                        var manifest = JobManifestService.LoadOrInitializeManifest(TranslationOrchestrator.CurrentJobDirectory, options, config, manifestPath);
                        await ConsoleErrorAnalysisService.PromptForErrorAnalysisAsync(manifest, config);
                    }
                }

                AppLogger.Info($"Program Main exiting with code: {exitCode}");
                return exitCode;
            }
            finally
            {
                AppLogger.Shutdown();
            }
        }
    }
}
