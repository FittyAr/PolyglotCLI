using Microsoft.AspNetCore.Components;

namespace PolyglotCLI.web.Components.Dialogs.JobDetails.Parts;

public partial class JobOcrTextViewer : ComponentBase
{
    [Parameter]
    public string? Text { get; set; }
}
