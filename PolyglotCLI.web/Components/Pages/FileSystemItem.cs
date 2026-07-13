namespace PolyglotCLI.web.Components.Pages;

public class FileSystemItem
{
    public bool IsDirectory { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
    public string Mode { get; set; } = "text";
    public string PageRange { get; set; } = "all";
    public string Extension { get; set; } = string.Empty;
}
