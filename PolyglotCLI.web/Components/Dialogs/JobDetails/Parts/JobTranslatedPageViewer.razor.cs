using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace PolyglotCLI.web.Components.Dialogs.JobDetails.Parts;

public partial class JobTranslatedPageViewer : ComponentBase
{
    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public bool IsEditingTranslation { get; set; } = true;

    [Parameter]
    public EventCallback OnToggleEditing { get; set; }

    [Parameter]
    public EventCallback<string> OnTranslationChanged { get; set; }

    private Task ToggleEditing() => OnToggleEditing.InvokeAsync();

    private Task NotifyTranslationChanged(string value) => OnTranslationChanged.InvokeAsync(value);

    private static string GetMarkdownForPreview(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Replace("&nbsp;", " ");
    }
}
