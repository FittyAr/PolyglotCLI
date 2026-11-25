using System.Text.RegularExpressions;

namespace PolyglotCLI
{
    /// <summary>
    /// Manejo centralizado de los bloques <c>&lt;think&gt;...&lt;/think&gt;</c>
    /// que emiten los modelos de razonamiento. Antes la misma regex estaba
    /// copiada en OcrService, TranslatorService, ReviewService y tres veces
    /// en el orquestador.
    /// </summary>
    public static class ThinkTagParser
    {
        private static readonly Regex ThinkBlockRegex = new(
            @"<think>([\s\S]*?)</think>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Separa la respuesta del modelo en texto útil (sin bloques
        /// think) y el razonamiento del primer bloque (o null si no hay).
        /// Si no hay bloques, el texto se devuelve sin modificar.
        /// </summary>
        public static (string Text, string? Thought) Split(string? raw)
        {
            string input = raw ?? string.Empty;
            var match = ThinkBlockRegex.Match(input);
            if (!match.Success)
            {
                return (input, null);
            }

            string thought = match.Groups[1].Value.Trim();
            string text = ThinkBlockRegex.Replace(input, string.Empty).Trim();
            return (text, string.IsNullOrEmpty(thought) ? null : thought);
        }

        /// <summary>
        /// Elimina todos los bloques think y recorta espacios.
        /// </summary>
        public static string Strip(string? raw)
        {
            return ThinkBlockRegex.Replace(raw ?? string.Empty, string.Empty).Trim();
        }

        /// <summary>
        /// Combina el razonamiento previo (OCR) con el de la traducción,
        /// con el mismo formato que usaba el orquestador.
        /// </summary>
        public static string? CombineThoughts(string? previousThought, string? translationThought)
        {
            if (string.IsNullOrEmpty(translationThought)) return previousThought;
            if (string.IsNullOrEmpty(previousThought)) return translationThought;
            return $"[OCR]:\n{previousThought}\n\n[Traducción]:\n{translationThought}";
        }
    }
}
