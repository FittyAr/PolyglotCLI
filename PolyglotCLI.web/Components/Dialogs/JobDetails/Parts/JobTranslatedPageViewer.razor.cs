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
        // RadzenMarkdown se renderiza en un iframe con srcdoc: el texto
        // que le pasamos NO pasa por el escape automático de Blazor, así
        // que un usuario (o un manifest importado) podría inyectar HTML
        // arbitrario, incluyendo <script>. Para mantener la utilidad del
        // preview (cursivas, negritas, enlaces en sintaxis markdown) pero
        // neutralizar cualquier etiqueta HTML, escapamos los caracteres
        // que delimitan tags. El caracter < usado en comparaciones tipo
        // "1 < 2" se verá como "1 &lt; 2", que es el comportamiento
        // esperado en un visor markdown seguro.
        return text
            .Replace("&nbsp;", " ")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
