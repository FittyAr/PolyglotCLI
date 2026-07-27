using System;
using System.IO;

namespace PolyglotCLI.Validation
{
    /// <summary>
    /// Defensa centralizada contra path traversal. Antes había dos
    /// implementaciones casi idénticas (en
    /// <c>PolyglotCLI.web.Services.JobDetails.JobArtifactsService</c>
    /// y en <c>PolyglotCLI.JobPackageService</c>); este helper es
    /// la única fuente de verdad.
    ///
    /// <para>Reglas:</para>
    /// <list type="bullet">
    ///   <item>Si <paramref name="path"/> es absoluto, se usa tal cual;
    ///     si es relativo, se combina con <paramref name="rootDir"/>.</item>
    ///   <item>El resultado se normaliza con <c>Path.GetFullPath</c>
    ///     (resuelve <c>..</c>, separadores, etc.).</item>
    ///   <item>El resultado debe ser igual al root, o estar
    ///     estrictamente <i>dentro</i> del root (prefijo +
    ///     <c>Path.DirectorySeparatorChar</c>). Un check naive de
    ///     <c>StartsWith(root)</c> aceptaría <c>/foo/bar</c> cuando
    ///     root es <c>/foo</c> — ese bug está explícitamente
    ///     prevenido acá.</item>
    ///   <item>Cualquier excepción (path malformado, IO error) se
    ///     traduce en <c>false</c> — fall closed, nunca fail open.</item>
    /// </list>
    /// </summary>
    public static class PathTraversalGuard
    {
        /// <summary>
        /// Resuelve <paramref name="path"/> contra <paramref name="rootDir"/>
        /// y verifica que el resultado sigue dentro del root. Si el path
        /// es absoluto, se interpreta absoluto (sin combinar con root).
        /// </summary>
        /// <returns>
        /// <c>true</c> si <paramref name="resolvedPath"/> es el root
        /// mismo o está estrictamente dentro; <c>false</c> en caso
        /// contrario (incluyendo paths vacíos, errores de IO, etc.).
        /// </returns>
        public static bool TryResolveInside(string rootDir, string path, out string resolvedPath)
        {
            resolvedPath = string.Empty;
            if (string.IsNullOrEmpty(rootDir) || string.IsNullOrEmpty(path)) return false;
            try
            {
                string root = Path.GetFullPath(rootDir);
                string combined = Path.IsPathRooted(path)
                    ? path
                    : Path.Combine(root, path);
                string resolved = Path.GetFullPath(combined);
                bool inside =
                    string.Equals(resolved, root, StringComparison.OrdinalIgnoreCase) ||
                    resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                if (!inside) return false;
                resolvedPath = resolved;
                return true;
            }
            catch
            {
                // Fail closed: cualquier excepción (path malformado,
                // caracteres no soportados en el FS, etc.) bloquea.
                return false;
            }
        }
    }
}
