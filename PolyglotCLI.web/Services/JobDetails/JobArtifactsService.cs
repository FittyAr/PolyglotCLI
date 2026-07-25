using System.Collections.Generic;
using System.IO;

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
    /// el archivo.
    /// </summary>
    string? ReadTextFile(string jobDir, string path, string errorPrefix = "Error al leer el archivo");

    /// <summary>
    /// Igual que <see cref="ReadTextFile"/> pero devuelve el contenido en
    /// base64. Usado por el visor de imágenes; la validación evita que un
    /// manifest manipulado exfiltre <c>config.json</c> u otros archivos
    /// del usuario al cliente Blazor.
    /// </summary>
    string? ReadFileAsBase64(string jobDir, string path, string errorPrefix = "Error al leer la imagen");
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

    public string? ReadTextFile(string jobDir, string path, string errorPrefix = "Error al leer el archivo")
    {
        if (!TryResolveInside(jobDir, path, out var fullPath))
        {
            return $"{errorPrefix}: ruta fuera del directorio del trabajo.";
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

    public string? ReadFileAsBase64(string jobDir, string path, string errorPrefix = "Error al leer la imagen")
    {
        if (!TryResolveInside(jobDir, path, out var fullPath))
        {
            return $"{errorPrefix}: ruta fuera del directorio del trabajo.";
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

    /// <summary>
    /// Resuelve <paramref name="path"/> contra <paramref name="jobDir"/>
    /// y verifica que el resultado sigue dentro de ese directorio. Bloquea
    /// paths absolutos, <c>..\..\..</c>, y symlinks que escapen.
    /// </summary>
    private static bool TryResolveInside(string jobDir, string path, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrEmpty(jobDir) || string.IsNullOrEmpty(path)) return false;
        try
        {
            string jobRoot = Path.GetFullPath(jobDir);
            // Si `path` es absoluto, combínalo con jobRoot (que lo descarta);
            // si es relativo, combínalo. Después resolvemos.
            string combined = Path.IsPathRooted(path)
                ? path
                : Path.Combine(jobRoot, path);
            string resolved = Path.GetFullPath(combined);
            // Comparación con comparador OrdinalIgnoreCase es segura porque
            // ambos lados ya están normalizados por GetFullPath.
            if (!resolved.StartsWith(jobRoot + Path.DirectorySeparatorChar, System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(resolved, jobRoot, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            fullPath = resolved;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
