using System.Collections.Generic;
using System.IO;

namespace PolyglotCLI.web.Services.JobDetails;

public interface IJobArtifactsService
{
    List<string> ListOutputFiles(string jobDir);
    List<string> ListTempImages(string jobDir);
    List<string> ListLogFiles(string jobDir);
    string? ReadTextFile(string path, string errorPrefix = "Error al leer el archivo");
    string? ReadFileAsBase64(string path, string errorPrefix = "Error al leer la imagen");
}

public class JobArtifactsService : IJobArtifactsService
{
    public List<string> ListOutputFiles(string jobDir)
    {
        return ListFiles(Path.Combine(jobDir, "outputs"));
    }

    public List<string> ListTempImages(string jobDir)
    {
        return ListFiles(Path.Combine(jobDir, "temp"), "*.png");
    }

    public List<string> ListLogFiles(string jobDir)
    {
        return ListFiles(Path.Combine(jobDir, "logs"));
    }

    public string? ReadTextFile(string path, string errorPrefix = "Error al leer el archivo")
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return $"{errorPrefix}: {ex.Message}";
        }
    }

    public string? ReadFileAsBase64(string path, string errorPrefix = "Error al leer la imagen")
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            return System.Convert.ToBase64String(bytes);
        }
        catch (Exception ex)
        {
            return $"{errorPrefix}: {ex.Message}";
        }
    }

    private static List<string> ListFiles(string directory, string searchPattern = "*")
    {
        var result = new List<string>();
        try
        {
            if (Directory.Exists(directory))
            {
                result.AddRange(Directory.GetFiles(directory, searchPattern));
            }
        }
        catch
        {
        }
        return result;
    }
}
