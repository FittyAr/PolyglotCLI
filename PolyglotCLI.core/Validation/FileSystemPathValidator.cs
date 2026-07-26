using System;
using System.IO;
using System.Linq;

namespace PolyglotCLI.Validation
{
    /// <summary>
    /// Validación de paths, file names y extensiones. El objetivo es
    /// prevenir path traversal y otros ataques de filesystem.
    ///
    /// <para>Por diseño, estos validadores NO rechazan paths que son
    /// válidos para el SO del usuario: en Windows se acepta
    /// 'C:\Users\foo\docs' y en Linux '/home/foo/docs'. Solo
    /// rechazan patrones que son vectores de ataque conocidos
    /// (parent-dir references, NUL injection, control chars).</para>
    /// </summary>
    public static class FileSystemPathValidator
    {
        // Tamaños máximos razonables. Estos límites evitan que un
        // config corrupto haga que la app intente crear un path de
        // 10GB que reviente el log o el filesystem.
        private const int MaxPathLength = 32_768;   // Windows MAX_PATH-ish
        private const int MaxFileNameLength = 255;  // Windows file name limit
        private const int MaxExtensionLength = 16;  // .html, .docx, .tar.gz

        // Caracteres de control (0x00-0x1F + 0x7F). NUL es el más
        // peligroso: en C/C++ truncaría el path silenciosamente. En
        // .NET tira excepción, pero igual lo bloqueamos.
        private static bool IsControlChar(char c) => c < 0x20 || c == 0x7F;

        /// <summary>
        /// Detecta si un path contiene secuencias de path traversal
        /// ('..' como segmento). NO se fija en el path RESUELTO (eso
        /// requeriría Path.GetFullPath y acceso al FS); se fija en
        /// el patrón textual.
        ///
        /// <para>Cubre:
        /// <c>..\foo</c>, <c>foo/../bar</c>, <c>..</c> solo, etc.
        /// Es case-insensitive porque Windows es case-insensitive.</para>
        /// </summary>
        public static bool ContainsPathTraversal(string? path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            // Split por / y \ y revisamos cada segmento.
            // Normalizamos: si un segmento es exactamente "..", es traversal.
            string[] segments = path.Split(new[] { '/', '\\' }, StringSplitOptions.None);
            return segments.Any(s => s == "..");
        }

        /// <summary>
        /// Valida un nombre de archivo (sin path, solo el nombre).
        /// No puede contener separadores de path ni caracteres de
        /// control. La longitud máxima es 255 (límite de Windows).
        /// </summary>
        public static ValidationResult<string> SanitizeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return ValidationResult<string>.Failure("El nombre del archivo está vacío.");

            if (name.Length > MaxFileNameLength)
                return ValidationResult<string>.Failure(
                    $"El nombre del archivo es demasiado largo ({name.Length} > {MaxFileNameLength}).");

            // No separadores de path en un nombre de archivo.
            if (name.Contains('/') || name.Contains('\\'))
                return ValidationResult<string>.Failure(
                    $"El nombre del archivo no puede contener separadores de path: '{name}'.");

            // No '..' como nombre completo (sería path traversal).
            if (name == "..")
                return ValidationResult<string>.Failure("El nombre '..' no está permitido.");

            // No caracteres de control (incluye NUL).
            for (int i = 0; i < name.Length; i++)
            {
                if (IsControlChar(name[i]))
                    return ValidationResult<string>.Failure(
                        $"El nombre del archivo contiene un carácter de control (posición {i}).");
            }

            // Windows no permite estos chars en nombres de archivo.
            char[] forbidden = { '<', '>', ':', '"', '|', '?', '*' };
            foreach (char c in forbidden)
            {
                if (name.Contains(c))
                    return ValidationResult<string>.Failure(
                        $"El nombre del archivo contiene un carácter reservado '{c}'.");
            }

            return ValidationResult<string>.Success(name);
        }

        /// <summary>
        /// Valida un path de directorio. Si <paramref name="mustExist"/>
        /// es true, también verifica que el directorio exista.
        /// </summary>
        public static ValidationResult<string> SanitizeDirectoryPath(string? path, bool mustExist = false)
        {
            if (string.IsNullOrWhiteSpace(path))
                return ValidationResult<string>.Failure("El path del directorio está vacío.");

            if (path.Length > MaxPathLength)
                return ValidationResult<string>.Failure(
                    $"El path es demasiado largo ({path.Length} > {MaxPathLength}).");

            // Caracteres de control
            for (int i = 0; i < path.Length; i++)
            {
                if (IsControlChar(path[i]))
                    return ValidationResult<string>.Failure(
                        $"El path contiene un carácter de control (posición {i}).");
            }

            // Path traversal
            if (ContainsPathTraversal(path))
                return ValidationResult<string>.Failure(
                    $"El path contiene secuencias de path traversal (no se permite '..'): '{path}'.");

            if (mustExist && !Directory.Exists(path))
                return ValidationResult<string>.Failure(
                    $"El directorio no existe: '{path}'.");

            return ValidationResult<string>.Success(path);
        }

        /// <summary>
        /// Valida una extensión de archivo. Debe empezar con '.' y
        /// no contener path separators ni caracteres raros. La
        /// longitud máxima es 16 chars (cubre extensiones comunes
        /// incluyendo las compuestas tipo .tar.gz).
        /// </summary>
        public static ValidationResult<string> SanitizeFileExtension(string? ext)
        {
            if (string.IsNullOrWhiteSpace(ext))
                return ValidationResult<string>.Failure("La extensión está vacía.");

            // Aceptamos tanto ".pdf" como "pdf" (con o sin punto).
            // Lo normalizamos para que siempre empiece con punto.
            string normalized = ext.StartsWith('.') ? ext : "." + ext;

            if (normalized.Length > MaxExtensionLength)
                return ValidationResult<string>.Failure(
                    $"La extensión es demasiado larga ({normalized.Length} > {MaxExtensionLength}).");

            if (normalized.Contains('/') || normalized.Contains('\\'))
                return ValidationResult<string>.Failure(
                    $"La extensión no puede contener separadores de path: '{ext}'.");

            for (int i = 0; i < normalized.Length; i++)
            {
                if (IsControlChar(normalized[i]))
                    return ValidationResult<string>.Failure(
                        $"La extensión contiene un carácter de control (posición {i}).");
            }

            // Después del punto: alfanum + '_', '-', y '.' (para
            // extensiones compuestas como .tar.gz, .min.js, etc.).
            for (int i = 1; i < normalized.Length; i++)
            {
                char c = normalized[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
                    return ValidationResult<string>.Failure(
                        $"La extensión contiene un carácter inválido '{c}' (solo se permiten letras, dígitos, '_', '-' y '.').");
            }

            return ValidationResult<string>.Success(normalized);
        }

        /// <summary>
        /// True si el path es absoluto (empieza con '/' en Unix o
        /// con letra:\ o \\ en Windows). Útil para diagnóstico
        /// ("¿el usuario está apuntando a un archivo global?").
        /// </summary>
        public static bool IsAbsolutePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            if (path.StartsWith('/') || path.StartsWith('\\')) return true; // Unix + Windows UNC

            // Windows: drive letter (C:\) o UNC (\\server\share)
            if (path.Length >= 3 &&
                char.IsLetter(path[0]) &&
                path[1] == ':' &&
                (path[2] == '\\' || path[2] == '/'))
                return true;

            return false;
        }
    }
}
