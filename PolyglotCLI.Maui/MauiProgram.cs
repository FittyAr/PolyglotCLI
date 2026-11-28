using Microsoft.Extensions.Logging;
using Radzen;
using PolyglotCLI;
using PolyglotCLI.Maui.Services;
using PolyglotCLI.web;
using PolyglotCLI.web.Localization;
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
			// La carpeta de datos de WebView2 (WEBVIEW2_USER_DATA_FOLDER) se
			// configura una sola vez en Platforms/Windows/App.xaml.cs, antes de
			// que WinUI inicialice cualquier WebView.

			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
				.UseMauiCommunityToolkit();

			// Inicializar configuracion y logger
			var config = AppConfig.Load();
			AppLogger.Initialize(config);
			builder.Services.AddSingleton(config);
			builder.Services.AddSingleton(new ApplicationMode(isWebMode: false));

			// Idioma de la interfaz (config o, si está vacío, el de Windows).
			Loc.ApplyLanguage(config.UiLanguage);
			builder.Services.AddSingleton(Loc.Instance);

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
