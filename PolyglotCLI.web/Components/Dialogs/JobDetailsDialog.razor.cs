using System.IO;
using Microsoft.AspNetCore.Components;
using PolyglotCLI;
using PolyglotCLI.web.Components.Dialogs.JobDetails;

namespace PolyglotCLI.web.Components.Dialogs;

public partial class JobDetailsDialog : ComponentBase
{
    [Parameter]
    public JobManifest Job { get; set; } = default!;

    private JobDetailsState State { get; set; } = new();

    protected override void OnParametersSet()
    {
        if (State.Job != Job)
        {
            State = new JobDetailsState
            {
                Job = Job,
                JobDir = Path.Combine(TranslationOrchestrator.GetJobsDirectory(), Job.JobId)
            };
        }
    }
}
