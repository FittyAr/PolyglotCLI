using System;

namespace PolyglotCLI.web.Components.Dialogs;

public class ProgressState
{
    public string Status { get; set; } = string.Empty;
    public event Action? OnChange;

    public void UpdateStatus(string status)
    {
        Status = status;
        OnChange?.Invoke();
    }
}
