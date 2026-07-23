using Microsoft.Extensions.Logging;
using Radzen;
using PolyglotCLI;
using PolyglotCLI.Maui.Services;
using PolyglotCLI.web;
using Cropper.Blazor.Extensions;
using CommunityToolkit.Maui;

namespace PolyglotCLI.Maui
{
	public static class MauiProgram
	{
		public static MauiApp CreateMauiApp()
		{
#if WINDOWS
			try
			{
				string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				string userDataFolder = System.IO.Path.Combine(localAppData, "PolyglotCLI", "WebView2");
				if (!System.IO.Directory.Exists(userDataFolder))
				{
					System.IO.Directory.CreateDirectory(userDataFolder);
				}
				Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataFolder);
			}
			catch { }
#endif

			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
				.UseMauiCommunityToolkit()
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

			// Servicio de empaquetado de trabajos específico de MAUI (file pickers nativos)
			builder.Services.AddSingleton<IJobPackageHost, MauiJobPackageHost>();

#if DEBUG
			builder.Services.AddBlazorWebViewDeveloperTools();
			builder.Logging.AddDebug();
#endif

			return builder.Build();
		}
	}
}
