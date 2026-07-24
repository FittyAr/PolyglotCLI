using Radzen;

namespace PolyglotCLI.web.Components.Dialogs.JobDetails;

public static class JobStatusBadgeStyle
{
    public static BadgeStyle GetBadgeStyle(string status)
    {
        return status switch
        {
            "Completed" => BadgeStyle.Success,
            "Failed" => BadgeStyle.Danger,
            "InProgress" => BadgeStyle.Warning,
            _ => BadgeStyle.Info
        };
    }
}
