using PolyglotCLI.web.Localization;
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

    /// <summary>
    /// Texto visible del estado persistido del trabajo. El valor guardado
    /// (Completed, Failed…) no cambia; solo se traduce lo que se muestra.
    /// </summary>
    public static string GetStatusText(string status)
    {
        return status switch
        {
            "Completed" => Loc.T("JobDetails.StatusCompleted"),
            "Failed" => Loc.T("JobDetails.StatusFailed"),
            "InProgress" => Loc.T("JobDetails.StatusInProgress"),
            "Cancelled" => Loc.T("JobDetails.StatusCancelled"),
            _ => status
        };
    }
}
