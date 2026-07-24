using System;
using System.Threading.Tasks;
using Cropper.Blazor.Components;
using Cropper.Blazor.Models;
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

    private CropperComponent? cropperRef;
    private int? _activePageNumber;
    private bool _isCropperReady;
    private bool _isDisposed;
    private bool _isReinitializing;

    private bool IsCropperInteractive => _isCropperReady && cropperRef != null && !_isDisposed;

    private string PageImageDataUrl =>
        string.IsNullOrEmpty(PageImageBase64) ? string.Empty : $"data:image/png;base64,{PageImageBase64}";

    private readonly Options cropperOptions = new()
    {
        ViewMode = ViewMode.Vm0,
        DragMode = "move",
        AutoCrop = false,
        AutoCropArea = 0,
        Background = false,
        CropBoxMovable = false,
        CropBoxResizable = false,
        Modal = false,
        Movable = true,
        Rotatable = false,
        Scalable = true,
        ToggleDragModeOnDblclick = false,
        Zoomable = true,
        ZoomOnWheel = true,
        ZoomOnTouch = true,
        WheelZoomRatio = 0.1m
    };

    private void OnCropperReady(Cropper.Blazor.Events.JSEventData<Cropper.Blazor.Events.CropReadyEvent.CropReadyEvent> _)
    {
        if (_isDisposed)
        {
            return;
        }

        _isCropperReady = true;
        _activePageNumber = PageNumber;
        InvokeAsync(StateHasChanged);
    }

    // El visor del PDF/imagen reusa el mismo <img> cuando cambia la página,
    // por lo que el evento onload del <img> se dispara para CADA nueva página.
    // Si dejamos que Cropper.Blazor auto-inicialice (IsAvailableInitCropper=true,
    // valor por defecto), apila un nuevo wrapper de canvas sobre el anterior en
    // el DOM. Aquí asumimos el control del ciclo de vida: destruimos cualquier
    // instancia previa y creamos una sola instancia nueva por página.
    private void HandleImageLoaded()
    {
        if (_isDisposed || cropperRef == null)
        {
            return;
        }

        if (!HasPageImage || string.IsNullOrEmpty(PageImageBase64))
        {
            return;
        }

        // InitCropper() también dispara OnLoadImageEvent de forma síncrona;
        // el flag evita reentrancia.
        if (_isReinitializing)
        {
            return;
        }

        _isReinitializing = true;
        try
        {
            if (_isCropperReady)
            {
                try
                {
                    cropperRef.Destroy();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cropper destroy error: {ex.Message}");
                }

                _isCropperReady = false;
                _activePageNumber = null;
            }

            cropperRef.InitCropper();
        }
        finally
        {
            _isReinitializing = false;
        }
    }

    private async Task HandleZoomIn() => await InvokeZoomAsync(+1);

    private async Task HandleZoomOut() => await InvokeZoomAsync(-1);

    private async Task InvokeZoomAsync(int direction)
    {
        if (!IsCropperInteractive || cropperRef == null)
        {
            await OnWarning.InvokeAsync();
            return;
        }
        try
        {
            cropperRef.Zoom(direction >= 0 ? 0.1m : -0.1m);
        }
        catch (Exception ex)
        {
            await OnError.InvokeAsync(ex.Message);
        }
    }

    private async Task HandleReset()
    {
        if (!IsCropperInteractive || cropperRef == null)
        {
            await OnWarning.InvokeAsync();
            return;
        }
        try
        {
            cropperRef.Reset();
        }
        catch (Exception ex)
        {
            await OnError.InvokeAsync(ex.Message);
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        _isCropperReady = false;
        cropperRef = null;
    }
}