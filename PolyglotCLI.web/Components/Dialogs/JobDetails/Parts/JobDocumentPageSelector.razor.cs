using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;

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

    private string _pageFilter = string.Empty;

    private string PageFilter
    {
        get => _pageFilter;
        set => _pageFilter = value ?? string.Empty;
    }

    private bool HasFilter => !string.IsNullOrWhiteSpace(_pageFilter);

    private List<DocumentPageData> FilteredPageDataList
    {
        get
        {
            if (PageDataList == null)
            {
                return new List<DocumentPageData>();
            }

            if (!HasFilter)
            {
                return PageDataList;
            }

            var trimmed = _pageFilter.Trim();
            return PageDataList
                .Where(p => p.PageNumber.ToString().Contains(trimmed, System.StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    private void OnPageFilterChanged(string value)
    {
        PageFilter = value;
    }
}