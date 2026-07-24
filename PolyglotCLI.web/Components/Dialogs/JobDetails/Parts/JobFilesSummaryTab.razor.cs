using Microsoft.AspNetCore.Components;
using PolyglotCLI;

namespace PolyglotCLI.web.Components.Dialogs.JobDetails.Parts;

public partial class JobFilesSummaryTab : ComponentBase
{
    [Parameter]
    public JobManifest Job { get; set; } = default!;
}
