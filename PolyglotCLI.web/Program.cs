using PolyglotCLI.web.Components;
using Radzen;
using PolyglotCLI;

namespace PolyglotCLI.web
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Inicializar la configuracion y el logger
            var config = AppConfig.Load();
            AppLogger.Initialize(config);
            builder.Services.AddSingleton(config);
            builder.Services.AddSingleton(new ApplicationMode(isWebMode: true));

            // Forzar la URL a localhost:5000
            builder.WebHost.UseUrls("http://localhost:5000");

            // Registrar componentes de Razor y Radzen
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents(options => {
                    options.DetailedErrors = true;
                })
                .AddHubOptions(options => {
                    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
                });
            builder.Services.AddRadzenComponents();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error", createScopeForErrors: true);
                app.UseHsts();
            }
            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();
            app.UseAntiforgery();
            app.UseStaticFiles();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("==================================================");
            Console.WriteLine("  Servidor PolyglotCLI iniciado en Modo Web");
            Console.WriteLine("  Abra su navegador en: http://localhost:5000");
            Console.WriteLine("  Presione Ctrl+C para detener el servidor");
            Console.WriteLine("==================================================");
            Console.ResetColor();

            app.Run();
        }
    }
}
