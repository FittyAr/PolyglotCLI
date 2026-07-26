using System;

namespace PolyglotCLI.Validation
{
    /// <summary>
    /// Clamping de valores numéricos. El objetivo es evitar que un
    /// config corrupto (ej: timeout = 0, temperature = 1000) haga
    /// que la app falle silenciosamente o DoS-e el LLM. Cada
    /// método clampea al rango razonable y devuelve el valor
    /// saneado (con fallback al default si está fuera de rango).
    /// </summary>
    public static class NumericRangeValidator
    {
        // Timeouts: de 1 segundo a 1 hora. Cero o negativo
        // sería instant timeout; 1 hora es un límite generoso
        // para cualquier llamada a LLM.
        public const int MinTimeoutSeconds = 1;
        public const int MaxTimeoutSeconds = 3_600;
        public const int DefaultTimeoutSeconds = 300;

        // Temperatura: el rango soportado por la mayoría de los
        // modelos es [0, 2]. Algunos van hasta 0.0-0.7, otros
        // hasta 2.0 (OpenAI permite 0-2). Fuera de [0, 2] no
        // tiene sentido.
        public const double MinTemperature = 0.0;
        public const double MaxTemperature = 2.0;
        public const double DefaultTemperature = 0.3;

        // Chunk size: de 100 chars (inútil pero válido) a
        // 100K chars (suficiente para documentos grandes sin
        // reventar memoria).
        public const int MinChunkSize = 100;
        public const int MaxChunkSize = 100_000;
        public const int DefaultChunkSize = 6_000;

        // Chunk overlap: de 0 al 50% del chunk size. Más que
        // eso y el overlap empieza a dominar el texto.
        public const int DefaultChunkOverlap = 300;

        /// <summary>
        /// Clampea un timeout al rango [MinTimeoutSeconds,
        /// MaxTimeoutSeconds]. Si el input es NaN, &lt;= 0, o
        /// &gt; Max, devuelve el default.
        /// </summary>
        public static int ClampTimeout(int value, int defaultValue = DefaultTimeoutSeconds)
        {
            if (value < MinTimeoutSeconds || value > MaxTimeoutSeconds) return defaultValue;
            return value;
        }

        /// <summary>
        /// Clampea una temperatura al rango [MinTemperature,
        /// MaxTemperature]. Si es NaN, infinito, o fuera de
        /// rango, devuelve el default.
        /// </summary>
        public static double ClampTemperature(double value, double defaultValue = DefaultTemperature)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return defaultValue;
            if (value < MinTemperature) return MinTemperature;
            if (value > MaxTemperature) return MaxTemperature;
            return value;
        }

        /// <summary>
        /// Clampea un chunk size al rango [MinChunkSize,
        /// MaxChunkSize]. Default si está fuera de rango.
        /// </summary>
        public static int ClampChunkSize(int value, int defaultValue = DefaultChunkSize)
        {
            if (value < MinChunkSize || value > MaxChunkSize) return defaultValue;
            return value;
        }

        /// <summary>
        /// Clampea un chunk overlap: [0, chunkSize/2]. Más
        ///Overlap del 50% hace que el overlap domine el chunk.
        /// Si chunkSize <= 0, devuelve defaultValue.
        /// </summary>
        public static int ClampChunkOverlap(int value, int chunkSize, int defaultValue = DefaultChunkOverlap)
        {
            if (chunkSize <= 0) return defaultValue;
            int maxOverlap = chunkSize / 2;
            if (value < 0) return 0;
            if (value > maxOverlap) return maxOverlap;
            return value;
        }
    }
}
