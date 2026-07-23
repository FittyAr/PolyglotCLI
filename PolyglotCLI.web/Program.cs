using PolyglotCLI.web.Components;
using Radzen;
using PolyglotCLI;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

// Comprobar si se solicita el modo web/servidor clasico por parametro
bool isWebMode = args.Contains("--web") || args.Contains("-web") || args.Contains("-w") || args.Contains("--server");

var builder = WebApplication.CreateBuilder(args);

// Inicializar la configuracion y el logger
var config = AppConfig.Load();
AppLogger.Initialize(config);
builder.Services.AddSingleton(config);

// Forzar la URL a localhost:5000
builder.WebHost.UseUrls("http://localhost:5000");

// Registrar componentes de Razor y Radzen
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRadzenComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

if (isWebMode)
{
    // Modo Web: Ejecutar como servidor clasico de consola
    app.Run();
}
else
{
    // Modo Ventana: Arrancar Blazor Server en segundo plano y abrir ventana nativa WebView2
    var appTask = app.RunAsync();

    ApplicationConfiguration.Initialize();

    var mainForm = new Form
    {
        Text = "PolyglotCLI - Traductor Documental con IA",
        Width = 1280,
        Height = 850,
        StartPosition = FormStartPosition.CenterScreen
    };

    // Cargar icono oficial de la aplicacion
    try
    {
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "app.ico");
        if (File.Exists(iconPath))
        {
            mainForm.Icon = new Icon(iconPath);
        }
    }
    catch { }

    var webView = new Microsoft.Web.WebView2.WinForms.WebView2
    {
        Dock = DockStyle.Fill
    };

    mainForm.Controls.Add(webView);

    mainForm.Load += async (s, e) =>
    {
        try
        {
            await webView.EnsureCoreWebView2Async();
            webView.Source = new Uri("http://localhost:5000");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al inicializar el visor web (WebView2): {ex.Message}\nAsegurese de tener instalado el WebView2 Runtime de Microsoft.", "Error de Inicializacion", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }
    };

    mainForm.FormClosed += async (s, e) =>
    {
        try
        {
            await app.StopAsync();
        }
        catch { }
        Application.Exit();
    };

    Application.Run(mainForm);
}
