using System.Collections.Generic;
using System.IO;
using PolyglotCLI.Validation;
using PolyglotCLI.web.Localization;

namespace PolyglotCLI.web.Services.JobDetails;

public interface IJobArtifactsService
{
    List<string> ListOutputFiles(string jobDir);
    List<string> ListTempImages(string jobDir);
    List<string> ListLogFiles(string jobDir);

    /// <summary>
    /// Lee un archivo de texto garantizando que el path resuelto queda
    /// dentro de <paramref name="jobDir"/>. Si el path apunta fuera del
    /// directorio del trabajo (path traversal, rutas absolutas a
    /// <c>config.json</c>, etc.) devuelve el prefijo de error sin abrir
    /// el archivo. <paramref name="errorPrefix"/> nulo = texto genérico
    /// localizado.
    /// </summary>
    string? ReadTextFile(string jobDir, string path, string? errorPrefix = null);

    /// <summary>
    /// Igual que <see cref="ReadTextFile"/> pero devuelve el contenido en
    /// base64. Usado por el visor de imágenes; la validación evita que un
    /// manifest manipulado exfiltre <c>config.json</c> u otros archivos
    /// del usuario al cliente Blazor.
    /// </summary>
    string? ReadFileAsBase64(string jobDir, string path, string? errorPrefix = null);
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

    public string? ReadTextFile(string jobDir, string path, string? errorPrefix = null)
    {
        errorPrefix ??= Loc.T("Services.FileReadError");
        if (!PathTraversalGuard.TryResolveInside(jobDir, path, out var fullPath))
        {
            return Loc.T("Services.PathOutsideJobDir", errorPrefix);
        }
        try
        {
            return File.ReadAllText(fullPath);
        }
        catch (Exception ex)
        {
            return $"{errorPrefix}: {ex.Message}";
        }
    }

    public string? ReadFileAsBase64(string jobDir, string path, string? errorPrefix = null)
    {
        errorPrefix ??= Loc.T("Services.ImageReadError");
        if (!PathTraversalGuard.TryResolveInside(jobDir, path, out var fullPath))
        {
            return Loc.T("Services.PathOutsideJobDir", errorPrefix);
        }
        try
        {
            byte[] bytes = File.ReadAllBytes(fullPath);
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
