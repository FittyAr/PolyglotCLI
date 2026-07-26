using System;
using System.Linq;

namespace PolyglotCLI.Validation
{
    /// <summary>
    /// Validación de nombres de modelos LLM y de proveedores. El
    /// objetivo es detectar typos y corrupciones sin bloquear
    /// providers legítimos (decisión: accept-any, ver
    /// docs/input-validation-plan.md).
    ///
    /// <para>Los nombres de modelos reales suelen tener
    /// <c>[org]/[model]:[tag]</c> (ej: "qwen/qwen2.5-7b:free",
    /// "llama3.1:8b-instruct-q4_K_M"). Permitimos alfanum + un set
    /// conservador de separadores: <c>.</c>, <c>-</c>, <c>_</c>,
    /// <c>/</c>, <c>:</c>, <c>+</c>. No whitespace, no control
    /// chars, no shell metachars.</para>
    /// </summary>
    public static class ModelNameValidator
    {
        private const int MaxNameLength = 200;

        // Shell metachars y otros caracteres que no deberían estar
        // en un nombre de modelo. Si los encontramos, es o un
        // typo o un intento de inyección.
        private static readonly char[] ForbiddenChars = { ' ', '\t', '\n', '\r', '\\',
            '\'', '"', '`', '$', '&', '|', ';', '<', '>', '(', ')',
            '{', '}', '[', ']', '*', '?', '!', '~' };

        /// <summary>
        /// Valida un nombre de modelo LLM. Permisivo: acepta
        /// <c>qwen/qwen2.5-7b:free</c>, <c>llama3.1:8b-instruct</c>,
        /// <c>claude-3-5-sonnet-20241022</c>, etc. Rechaza: vacío,
        /// control chars, whitespace, shell metachars, longitud
        /// absurda.
        /// </summary>
        public static ValidationResult<string> SanitizeModelName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return ValidationResult<string>.Failure("El nombre del modelo está vacío.");

            string trimmed = name.Trim();
            if (trimmed.Length == 0)
                return ValidationResult<string>.Failure("El nombre del modelo está vacío (solo whitespace).");

            if (trimmed.Length > MaxNameLength)
                return ValidationResult<string>.Failure(
                    $"El nombre del modelo es demasiado largo ({trimmed.Length} > {MaxNameLength}).");

            foreach (char c in ForbiddenChars)
            {
                if (trimmed.Contains(c))
                    return ValidationResult<string>.Failure(
                        $"El nombre del modelo contiene un carácter inválido '{c}'.");
            }

            // El primer y último char no pueden ser un separador.
            // Esto evita "/foo" o "foo/" que rompen parsers.
            if (trimmed[0] == '.' || trimmed[0] == '-' || trimmed[0] == '_' ||
                trimmed[0] == '/' || trimmed[0] == ':' || trimmed[0] == '+')
                return ValidationResult<string>.Failure(
                    $"El nombre del modelo no puede empezar con '{trimmed[0]}'.");

            if (trimmed[^1] == '/' || trimmed[^1] == ':' || trimmed[^1] == '+')
                return ValidationResult<string>.Failure(
                    $"El nombre del modelo no puede terminar con '{trimmed[^1]}'.");

            return ValidationResult<string>.Success(trimmed);
        }

        /// <summary>
        /// Más estricto que SanitizeModelName: solo alfanum +
        /// underscore. Para nombres de proveedores (que son menos
        /// flexibles: "OpenAI", "Ollama", "LM_Studio", etc.).
        /// </summary>
        public static ValidationResult<string> SanitizeProviderName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return ValidationResult<string>.Failure("El nombre del proveedor está vacío.");

            string trimmed = name.Trim();
            if (trimmed.Length > 50)
                return ValidationResult<string>.Failure(
                    $"El nombre del proveedor es demasiado largo ({trimmed.Length} > 50).");

            // Solo alfanum, guion bajo, guion, espacio (para "LM
            // Studio"). No slash, no dos puntos, no punto.
            foreach (char c in trimmed)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != ' ')
                    return ValidationResult<string>.Failure(
                        $"El nombre del proveedor contiene un carácter inválido '{c}' " +
                        $"(solo letras, dígitos, '_', '-' y espacio).");
            }

            return ValidationResult<string>.Success(trimmed);
        }
    }
}
