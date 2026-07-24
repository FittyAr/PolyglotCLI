using System.Collections.Generic;
using PolyglotCLI;

namespace PolyglotCLI.web.Components.Dialogs.JobDetails;

public class JobDetailsState
{
    public JobManifest Job { get; set; } = default!;
    public string JobDir { get; set; } = string.Empty;
    public bool IsDetailsCollapsed { get; set; } = true;
    public Dictionary<int, string> PendingTranslationEdits { get; } = new Dictionary<int, string>();
}
