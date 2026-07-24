using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using PolyglotCLI;

namespace PolyglotCLI.web.Components.Dialogs.JobDetails.Parts;

public partial class JobDetailsOverviewPanel : ComponentBase
{
    [Parameter]
    public JobManifest Job { get; set; } = default!;

    [Parameter]
    public bool IsDetailsCollapsed { get; set; } = true;

    [Parameter]
    public EventCallback<bool> IsDetailsCollapsedChanged { get; set; }

    private Task HandleCollapse() => IsDetailsCollapsedChanged.InvokeAsync(true);

    private Task HandleExpand() => IsDetailsCollapsedChanged.InvokeAsync(false);
}
