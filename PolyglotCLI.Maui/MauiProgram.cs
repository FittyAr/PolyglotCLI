using Microsoft.Extensions.Logging;
using Radzen;
using PolyglotCLI;
using PolyglotCLI.web;
using Cropper.Blazor.Extensions;

namespace PolyglotCLI.Maui
{
	public static class MauiProgram
	{
		public static MauiApp CreateMauiApp()
		{
			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				});

			// Inicializar configuracion y logger
			var config = AppConfig.Load();
			AppLogger.Initialize(config);
			builder.Services.AddSingleton(config);
			builder.Services.AddSingleton(new ApplicationMode(isWebMode: false));

			builder.Services.AddMauiBlazorWebView();
			builder.Services.AddRadzenComponents();
			builder.Services.AddCropper();

#if DEBUG
			builder.Services.AddBlazorWebViewDeveloperTools();
			builder.Logging.AddDebug();
#endif

			return builder.Build();
		}
	}
}
