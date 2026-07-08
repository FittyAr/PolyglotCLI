using System;
using System.Threading.Tasks;

namespace PolyglotCLI
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("==================================================");
            Console.WriteLine("                  PolyglotCLI                     ");
            Console.WriteLine("==================================================");

            // Load Configuration
            var config = AppConfig.Load();

            // 1. Parse command line options or launch interactive menu
            CommandLineOptions? options;
            if (args.Length == 0)
            {
                options = await InteractiveMenu.RunAsync(config);
                if (options == null)
                {
                    return 0; // Cancelled by user
                }
            }
            else
            {
                options = CommandLineOptions.Parse(args, config);
                if (options == null)
                {
                    return 1;
                }
            }

            // 2. Delegate execution orchestration to TranslationOrchestrator
            return await TranslationOrchestrator.ExecuteAsync(options, config);
        }
    }
}
