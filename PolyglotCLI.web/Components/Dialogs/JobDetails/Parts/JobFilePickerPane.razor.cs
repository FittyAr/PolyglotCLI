using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using PolyglotCLI.web.Localization;

namespace PolyglotCLI.web.Components.Dialogs.JobDetails.Parts;

public partial class JobFilePickerPane : ComponentBase
{
    [Parameter]
    public List<string>? Items { get; set; }

    [Parameter]
    public string ListTitle { get; set; } = string.Empty;

    [Parameter]
    public string EmptyMessage { get; set; } = Loc.T("JobDetails.NoItems");

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
    public string PreviewTitle { get; set; } = Loc.T("JobDetails.PreviewTitle");

    [Parameter]
    public string DefaultSelectionHint { get; set; } = Loc.T("JobDetails.SelectFileHint");

    [Parameter]
    public string PreviewColor { get; set; } = "#cbd5e1";

    [Parameter]
    public string PreviewEmptyMessage { get; set; } = Loc.T("JobDetails.SelectItemHint");

    [Parameter]
    public bool ShowImagePreview { get; set; }

    [Parameter]
    public EventCallback<string> OnFileSelected { get; set; }
}
