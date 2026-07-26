using System;

namespace PolyglotCLI.Validation
{
    /// <summary>
    /// Validación de prompts. Decisión: length + control chars
    /// solamente, sin heurísticas de prompt injection. Razón:
    /// PolyglotCLI procesa texto fuente que el usuario quiere
    /// traducir — heurísticas tipo "ignore previous" o
    /// "&lt;|im_start|&gt;" darían falsos positivos sobre
    /// documentos que casualmente contienen esas frases.
    ///
    /// <para>Lo que validamos: longitud máxima (evita DoS) y
    /// caracteres de control (NUL, newline crudo, etc. que
    /// pueden romper parsers downstream).</para>
    /// </summary>
    public static class PromptValidator
    {
        /// <summary>
        /// Default 50.000 chars. Aproximadamente 12K tokens para
        /// un modelo típico. Suficiente para cualquier prompt
        /// razonable (los prompts del sistema de PolyglotCLI
        /// tienen típicamente 500-2000 chars).
        /// </summary>
        public const int DefaultMaxLength = 50_000;

        /// <summary>
        /// Valida y normaliza un prompt. Si supera la longitud
        /// máxima o contiene caracteres de control (excepto
        /// tab/newline/CR que son normales en texto), devuelve
        /// error.
        /// </summary>
        public static ValidationResult<string> SanitizePrompt(string? prompt, int maxLength = DefaultMaxLength)
        {
            // null es un input legítimo ("no hay prompt"). Lo
            // aceptamos como string vacío en vez de fallar.
            if (prompt == null)
                return ValidationResult<string>.Success(string.Empty);

            if (prompt.Length > maxLength)
                return ValidationResult<string>.Failure(
                    $"El prompt es demasiado largo ({prompt.Length} > {maxLength} caracteres). " +
                    $"Considerá reducirlo o partirlo en chunks.");

            // Caracteres de control: NUL es el más peligroso. Tab,
            // newline y CR son normales en texto. Otros (0x01-0x1F
            // excepto 0x09/0x0A/0x0D, más 0x7F) son raros en prompts
            // legítimos.
            for (int i = 0; i < prompt.Length; i++)
            {
                char c = prompt[i];
                if (c == 0x00)
                    return ValidationResult<string>.Failure(
                        $"El prompt contiene un carácter NUL (posición {i}). " +
                        $"Esto puede truncar el string al pasarlo al LLM.");

                if (c < 0x20 && c != '\t' && c != '\n' && c != '\r')
                    return ValidationResult<string>.Failure(
                        $"El prompt contiene un carácter de control 0x{(int)c:X2} (posición {i}).");

                if (c == 0x7F)
                    return ValidationResult<string>.Failure(
                        $"El prompt contiene un carácter DEL (0x7F, posición {i}).");
            }

            return ValidationResult<string>.Success(prompt);
        }

        /// <summary>
        /// Placeholder para detección de prompt injection.
        /// <b>No implementado por diseño</b> (ver decisión del
        /// plan): heurísticas de injection darían falsos positivos
        /// sobre documentos a traducir que casualmente contienen
        /// strings como "ignore previous instructions".
        ///
        /// <para>Si en algún momento vemos intentos reales de
        /// injection, implementar acá. Mientras tanto, devuelve
        /// array vacío.</para>
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<string> DetectInjectionAttempts(string? prompt)
        {
            // Intencionalmente vacío. Ver comentario arriba.
            return Array.Empty<string>();
        }
    }
}
