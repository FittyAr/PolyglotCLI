using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace PolyglotCLI.Maui.WinUI
{
	/// <summary>
	/// Punto de entrada WinUI de la app de escritorio. Antes de levantar MAUI:
	///   1. Engancha los manejadores globales de excepciones no controladas
	///      (AppDomain, TaskScheduler y WinUI) para que queden en el log.
	///   2. Fija la carpeta de datos de WebView2 bajo FittyAr\PolyglotCLI.
	///   3. Verifica que el runtime de WebView2 esté instalado; si no lo está,
	///      informa al usuario con el enlace de descarga en lugar de fallar
	///      con una ventana en blanco.
	/// </summary>
	public partial class App : MauiWinUIApplication
	{
		private const string WebView2DownloadUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

		public App()
		{
			RegisterGlobalExceptionHandlers();
			ConfigureWebView2UserDataFolder();
			EnsureWebView2RuntimeOrExit();

			this.InitializeComponent();
		}

		protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

		private void RegisterGlobalExceptionHandlers()
		{
			AppDomain.CurrentDomain.UnhandledException += (_, e) =>
			{
				AppLogger.Fatal(
					$"Excepción no controlada (AppDomain, IsTerminating={e.IsTerminating}).",
					e.ExceptionObject as Exception);
				if (e.IsTerminating)
				{
					AppLogger.Shutdown();
				}
			};

			TaskScheduler.UnobservedTaskException += (_, e) =>
			{
				AppLogger.Error("Excepción no observada en una Task.", e.Exception);
				e.SetObserved();
			};

			// Excepciones del hilo de UI de WinUI. No se marcan como manejadas:
			// solo se registran para diagnóstico y se mantiene el comportamiento
			// por defecto del framework.
			this.UnhandledException += (_, e) =>
			{
				AppLogger.Fatal($"Excepción no controlada en WinUI: {e.Message}", e.Exception);
			};
		}

		private static void ConfigureWebView2UserDataFolder()
		{
			try
			{
				string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				// Estructura {desarrollador}\{programa}: el cache de
				// WebView2 también vive bajo FittyAr\PolyglotCLI para
				// no plantar la app directo en %LocalAppData%.
				string userDataFolder = Path.Combine(localAppData, "FittyAr", "PolyglotCLI", "WebView2");
				Directory.CreateDirectory(userDataFolder);
				Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataFolder);
			}
			catch (Exception ex)
			{
				// WebView2 usará su carpeta por defecto.
				AppLogger.Warn($"No se pudo configurar WEBVIEW2_USER_DATA_FOLDER; se usará la carpeta por defecto de WebView2. {ex.Message}");
			}
		}

		private static void EnsureWebView2RuntimeOrExit()
		{
			// En la proyección WinRT de WebView2 (Windows App SDK), si el runtime
			// no está instalado GetAvailableBrowserVersionString lanza una
			// COMException con HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND).
			const int HResultFileNotFound = unchecked((int)0x80070002);
			try
			{
				string? version = CoreWebView2Environment.GetAvailableBrowserVersionString();
				if (!string.IsNullOrWhiteSpace(version))
				{
					AppLogger.Info($"Runtime de WebView2 detectado: {version}");
					return;
				}
				AppLogger.Error("No se encontró el runtime de Microsoft Edge WebView2 (versión vacía).");
			}
			catch (Exception ex) when (ex.HResult == HResultFileNotFound)
			{
				AppLogger.Error("No se encontró el runtime de Microsoft Edge WebView2.", ex);
			}
			catch (Exception ex)
			{
				// Cualquier otro error en la detección no debe impedir el arranque:
				// dejamos que BlazorWebView lo intente y, si falla, quedará en el log.
				AppLogger.Warn($"No se pudo verificar el runtime de WebView2: {ex.Message}");
				return;
			}

			const uint MB_YESNO = 0x00000004;
			const uint MB_ICONERROR = 0x00000010;
			const int IDYES = 6;
			int answer = MessageBoxW(
				IntPtr.Zero,
				"PolyglotCLI necesita Microsoft Edge WebView2 Runtime, que no está instalado en este equipo.\n\n" +
				"¿Desea abrir la página de descarga oficial de Microsoft?\n" + WebView2DownloadUrl,
				"PolyglotCLI - Falta WebView2 Runtime",
				MB_YESNO | MB_ICONERROR);

			if (answer == IDYES)
			{
				try
				{
					Process.Start(new ProcessStartInfo(WebView2DownloadUrl) { UseShellExecute = true });
				}
				catch (Exception ex)
				{
					AppLogger.Error("No se pudo abrir el navegador con la descarga de WebView2.", ex);
				}
			}

			AppLogger.Shutdown();
			Environment.Exit(1);
		}

		[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
		private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
	}
}
