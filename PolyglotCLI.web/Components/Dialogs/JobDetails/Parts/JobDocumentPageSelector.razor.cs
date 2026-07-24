using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using PolyglotCLI;

namespace PolyglotCLI.web.Components.Dialogs.JobDetails.Parts;

public partial class JobDocumentPageSelector : ComponentBase
{
    [Parameter]
    public List<JsonFileEntry>? JsonFiles { get; set; }

    [Parameter]
    public JsonFileEntry? SelectedJsonEntry { get; set; }

    [Parameter]
    public EventCallback<JsonFileEntry> OnJsonFileSelected { get; set; }

    [Parameter]
    public List<DocumentPageData>? PageDataList { get; set; }

    [Parameter]
    public DocumentPageData? SelectedPageData { get; set; }

    [Parameter]
    public EventCallback<DocumentPageData> OnPageSelected { get; set; }

    [Parameter]
    public IReadOnlyDictionary<int, string> PendingTranslationEdits { get; set; } = new Dictionary<int, string>();
}
