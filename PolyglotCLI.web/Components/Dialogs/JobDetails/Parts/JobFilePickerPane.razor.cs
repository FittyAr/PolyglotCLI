using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace PolyglotCLI.web.Components.Dialogs.JobDetails.Parts;

public partial class JobFilePickerPane : ComponentBase
{
    [Parameter]
    public List<string>? Items { get; set; }

    [Parameter]
    public string ListTitle { get; set; } = string.Empty;

    [Parameter]
    public string EmptyMessage { get; set; } = "No hay elementos disponibles.";

    [Parameter]
    public string ListStyle { get; set; } = "width: 100%; flex-grow: 1; height: 100%; min-height: 0; background: transparent; border: none;";

    [Parameter]
    public Func<string, string> GetIcon { get; set; } = _ => "description";

    [Parameter]
    public string IconStyle { get; set; } = "font-size: 1.2rem; color: #6366f1;";

    [Parameter]
    public string? SelectedFileName { get; set; }

    [Parameter]
    public string? PreviewContent { get; set; }

    [Parameter]
    public string PreviewTitle { get; set; } = "Previsualización";

    [Parameter]
    public string DefaultSelectionHint { get; set; } = "Selecciona un archivo";

    [Parameter]
    public string PreviewColor { get; set; } = "#cbd5e1";

    [Parameter]
    public string PreviewEmptyMessage { get; set; } = "Selecciona un elemento para ver su contenido.";

    [Parameter]
    public bool ShowImagePreview { get; set; }

    [Parameter]
    public EventCallback<string> OnFileSelected { get; set; }
}
