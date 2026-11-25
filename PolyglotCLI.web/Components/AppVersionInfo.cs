using System.Reflection;

namespace PolyglotCLI.web.Components;

/// <summary>
/// Versión de la aplicación en ejecución. Se lee del ensamblado de entrada
/// (PolyglotCLI.exe en modo web, PolyglotCLI.Maui.exe en escritorio) para
/// que ambos hosts muestren la versión con la que fueron publicados.
/// Este archivo se compila en ambos proyectos (MAUI lo enlaza desde
/// Components/**).
/// </summary>
public static class AppVersionInfo
{
    /// <summary>Versión semántica sin metadata de build (p.ej. "1.2.2").</summary>
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var asm = Assembly.GetEntryAssembly() ?? typeof(AppVersionInfo).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            // "1.2.2+abcdef..." -> "1.2.2"
            int plusIdx = info.IndexOf('+');
            return plusIdx > 0 ? info[..plusIdx] : info;
        }
        return asm.GetName().Version?.ToString(3) ?? "1.0.0";
    }
}
