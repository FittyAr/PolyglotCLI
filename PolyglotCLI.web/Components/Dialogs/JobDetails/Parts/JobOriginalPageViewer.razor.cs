using System;
using System.Threading.Tasks;
using BlazorPanzoom;
using Microsoft.AspNetCore.Components;

namespace PolyglotCLI.web.Components.Dialogs.JobDetails.Parts;

public partial class JobOriginalPageViewer : ComponentBase
{
    [Parameter]
    public int? PageNumber { get; set; }

    [Parameter]
    public bool HasPageImage { get; set; }

    [Parameter]
    public string? PageImageBase64 { get; set; }

    [Parameter]
    public EventCallback OnWarning { get; set; }

    [Parameter]
    public EventCallback<string> OnError { get; set; }

    private Panzoom? panzoomRef;
    private int? _activePageNumber;
    private bool _isPanzoomReady;
    private bool _isDisposed;

    private bool IsPanzoomInteractive => _isPanzoomReady && panzoomRef != null && !_isDisposed;

    private string PageImageDataUrl =>
        string.IsNullOrEmpty(PageImageBase64) ? string.Empty : $"data:image/png;base64,{PageImageBase64}";

    private readonly PanzoomOptions panzoomOptions = new()
    {
        // Canvas = true hace que el pan/zoom se aplique al wrapper completo,
        // no solo a la imagen, dejando margen para arrastrar fuera del <img>.
        Canvas = true,
        // El visor es solo pan/zoom: sin animación para que los botones
        // Acercar/Alejar/Restablecer se sientan instantáneos.
        Animate = false,
    };

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            // BlazorPanzoom inicializa su instancia JS en OnAfterRenderAsync;
            // marcamos el visor como interactivo al primer render.
            _isPanzoomReady = true;
            _activePageNumber = PageNumber;
        }
    }

    // El visor reusa el mismo <img> cuando cambia la página, así que el evento
    // onload del <img> se dispara para CADA nueva página. Llamamos a Reset()
    // para que el pan/zoom vuelvan a la posición inicial en cada cambio.
    private async Task OnImageLoaded()
    {
        if (_isDisposed || panzoomRef == null)
        {
            return;
        }

        if (!HasPageImage || string.IsNullOrEmpty(PageImageBase64))
        {
            return;
        }

        // Race condition: el onload del <img> puede dispararse antes de que
        // BlazorPanzoom termine de inicializar su handle JS interno
        // (_jsPanzoomReference), lo que produce una NullReferenceException
        // transitoria. Reintentamos una vez con un pequeño delay; si sigue
        // fallando, la siguiente interacción del usuario (botón Restablecer,
        // cambio de página) ya lo va a tener listo.
        if (!await TryResetAsync())
        {
            await Task.Delay(100);
            await TryResetAsync();
        }
    }

    /// <summary>
    /// Intenta resetear el panzoom. Devuelve true si tuvo éxito, false si
    /// falló por una race condition con la inicialización JS de la librería.
    /// </summary>
    private async Task<bool> TryResetAsync()
    {
        if (_isDisposed || panzoomRef == null)
            return true;

        try
        {
            await panzoomRef.ResetAsync();
            return true;
        }
        catch (Exception ex)
        {
            // NRE típica: _jsPanzoomReference todavía es null. Otros errores
            // también pueden ocurrir; los silenciamos todos porque el
            // siguiente intento o la próxima interacción del usuario
            // terminarán funcionando.
            AppLogger.Debug($"Panzoom reset deferred (will retry): {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private async Task HandleZoomIn()
    {
        if (!IsPanzoomInteractive || panzoomRef == null)
        {
            await OnWarning.InvokeAsync();
            return;
        }
        try
        {
            await panzoomRef.ZoomInAsync();
        }
        catch (Exception ex)
        {
            await OnError.InvokeAsync(ex.Message);
        }
    }

    private async Task HandleZoomOut()
    {
        if (!IsPanzoomInteractive || panzoomRef == null)
        {
            await OnWarning.InvokeAsync();
            return;
        }
        try
        {
            await panzoomRef.ZoomOutAsync();
        }
        catch (Exception ex)
        {
            await OnError.InvokeAsync(ex.Message);
        }
    }

    private async Task HandleReset()
    {
        if (!IsPanzoomInteractive || panzoomRef == null)
        {
            await OnWarning.InvokeAsync();
            return;
        }
        try
        {
            await panzoomRef.ResetAsync();
        }
        catch (Exception ex)
        {
            await OnError.InvokeAsync(ex.Message);
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        _isPanzoomReady = false;
        panzoomRef = null;
    }
}
