using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PolyglotCLI;
using PolyglotCLI.web.Components.Dialogs.JobDetails;

namespace PolyglotCLI.web.Services.JobDetails;

public interface IJobPageVerifierService
{
    List<JsonFileEntry> ListJsonFiles(string jobDir, JobManifest? job);
    List<DocumentPageData> LoadPageData(string jobDir, string jsonFileName);
    string? LoadPageImageBase64(string jobDir, string docName, int pageNumber);
}

public class JobPageVerifierService : IJobPageVerifierService
{
    public List<JsonFileEntry> ListJsonFiles(string jobDir, JobManifest? job)
    {
        var entries = new List<JsonFileEntry>();
        try
        {
            string dataPath = Path.Combine(jobDir, "data");
            if (!Directory.Exists(dataPath))
            {
                return entries;
            }

            var files = Directory.GetFiles(dataPath, "*_data.json");
            foreach (var filePath in files)
            {
                string jsonFileName = Path.GetFileName(filePath);
                string normalized = jsonFileName.Replace("_data.json", string.Empty, System.StringComparison.OrdinalIgnoreCase);

                var matchedManifest = job?.Files?.FirstOrDefault(f =>
                    string.Equals(Path.GetFileNameWithoutExtension(f.SourceFilePath), normalized, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(f.NormalizedFileName, normalized, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetFileNameWithoutExtension(f.OriginalFileName), normalized, System.StringComparison.OrdinalIgnoreCase)
                );

                string display = matchedManifest != null && !string.IsNullOrEmpty(matchedManifest.OriginalFileName)
                    ? matchedManifest.OriginalFileName
                    : normalized;

                int pageCount = 0;
                try
                {
                    string jsonStr = File.ReadAllText(filePath);
                    var pages = JsonSerializer.Deserialize<List<DocumentPageData>>(jsonStr);
                    pageCount = pages?.Count ?? 0;
                }
                catch
                {
                }

                entries.Add(new JsonFileEntry
                {
                    JsonFileName = jsonFileName,
                    DisplayName = display,
                    PageCount = pageCount
                });
            }
        }
        catch
        {
        }
        return entries;
    }

    public List<DocumentPageData> LoadPageData(string jobDir, string jsonFileName)
    {
        try
        {
            string filePath = Path.Combine(jobDir, "data", jsonFileName);
            if (File.Exists(filePath))
            {
                string jsonStr = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<List<DocumentPageData>>(jsonStr) ?? new List<DocumentPageData>();
            }
        }
        catch
        {
        }
        return new List<DocumentPageData>();
    }

    public string? LoadPageImageBase64(string jobDir, string docName, int pageNumber)
    {
        try
        {
            string pngPath = Path.Combine(jobDir, "temp", $"{docName}_page_{pageNumber}.png");
            if (File.Exists(pngPath))
            {
                byte[] bytes = File.ReadAllBytes(pngPath);
                return System.Convert.ToBase64String(bytes);
            }
        }
        catch
        {
        }
        return null;
    }
}
