using Microsoft.Extensions.Logging;
using Radzen;
using PolyglotCLI;
using PolyglotCLI.Maui.Services;
using PolyglotCLI.web;
using PolyglotCLI.web.Services.JobDetails;
using BlazorPanzoom;
using CommunityToolkit.Maui;
using static Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions;

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
				// Estructura {desarrollador}\{programa}: el cache de
				// WebView2 también vive bajo FittyAr\PolyglotCLI para
				// no plantar la app directo en %LocalAppData%.
				string userDataFolder = System.IO.Path.Combine(localAppData, "FittyAr", "PolyglotCLI", "WebView2");
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
			builder.Services.AddBlazorPanzoomServices();

			// Servicio de empaquetado de trabajos específico de MAUI (file pickers nativos)
			builder.Services.AddSingleton<IJobPackageHost, MauiJobPackageHost>();
			builder.Services.AddSingleton<IFolderPickerService, MauiFolderPickerService>();

			// Servicios del verificador de páginas: son lógica pura (filesystem + JSON)
			// sin dependencia de HTTP, así que se reutilizan idénticos en Web y MAUI.
			builder.Services.AddScoped<IJobArtifactsService, JobArtifactsService>();
			builder.Services.AddScoped<IJobPageVerifierService, JobPageVerifierService>();
			builder.Services.AddScoped<IJobPageEditService, JobPageEditService>();
			builder.Services.AddScoped<IJobPageReprocessService, JobPageReprocessService>();

#if DEBUG
			builder.Services.AddBlazorWebViewDeveloperTools();
			builder.Logging.AddDebug();
#endif

			return builder.Build();
		}
	}
}
