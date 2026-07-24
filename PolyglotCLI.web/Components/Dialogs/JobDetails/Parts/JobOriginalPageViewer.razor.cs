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
    private int? lastCropperPageNumber;

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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (cropperRef != null
            && PageNumber != lastCropperPageNumber
            && HasPageImage
            && !string.IsNullOrEmpty(PageImageBase64))
        {
            try
            {
                await cropperRef.ReplaceAsync(PageImageDataUrl, false);
                lastCropperPageNumber = PageNumber;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cropper replace error: {ex.Message}");
            }
        }
    }

    private void OnCropperReady(Cropper.Blazor.Events.JSEventData<Cropper.Blazor.Events.CropReadyEvent.CropReadyEvent> _)
    {
        lastCropperPageNumber = PageNumber;
    }

    private async Task HandleZoomIn() => await InvokeZoomAsync(+1);

    private async Task HandleZoomOut() => await InvokeZoomAsync(-1);

    private async Task InvokeZoomAsync(int direction)
    {
        if (cropperRef == null)
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
        if (cropperRef == null)
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
}
