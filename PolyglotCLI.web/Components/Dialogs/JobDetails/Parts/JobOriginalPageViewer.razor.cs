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

        try
        {
            await panzoomRef.ResetAsync();
        }
        catch (Exception ex)
        {
            // ResetAsync puede fallar en el primer render antes de que el
            // JS bundle de panzoom haya terminado de inicializar; lo ignoramos
            // y dejamos que la próxima interacción del usuario lo configure.
            Console.WriteLine($"Panzoom reset error: {ex.Message}");
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
