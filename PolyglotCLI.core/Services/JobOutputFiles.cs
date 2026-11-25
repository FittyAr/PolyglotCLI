using System;
using System.IO;

namespace PolyglotCLI
{
    /// <summary>
    /// Convenciones de nombres de los archivos de salida de un documento
    /// (<c>{nombre}_original.*</c> y <c>{nombre}_{idioma}.*</c>) y copia de
    /// esos archivos a la carpeta <c>outputs/</c> del job.
    /// </summary>
    public static class JobOutputFiles
    {
        public const string OriginalSuffix = "original";

        public static string GetTranslatedBaseName(string sourceFilePath, string targetLanguage)
            => $"{Path.GetFileNameWithoutExtension(sourceFilePath)}_{targetLanguage}";

        public static string GetOriginalBaseName(string sourceFilePath)
            => $"{Path.GetFileNameWithoutExtension(sourceFilePath)}_{OriginalSuffix}";

        public static string GetTranslatedMarkdownPath(string outputDirectory, string sourceFilePath, string targetLanguage)
            => Path.Combine(outputDirectory, GetTranslatedBaseName(sourceFilePath, targetLanguage) + ".md");

        public static string GetOriginalMarkdownPath(string outputDirectory, string sourceFilePath)
            => Path.Combine(outputDirectory, GetOriginalBaseName(sourceFilePath) + ".md");

        /// <summary>
        /// True si <paramref name="fileName"/> es una salida de ESTE
        /// documento: su nombre sin extensión es exactamente
        /// <c>{nombre}_original</c> o <c>{nombre}_{idioma}</c>. Un prefijo
        /// (StartsWith) copiaba salidas de otros documentos que empiezan
        /// igual ("doc" vs "doc2_Spanish.md").
        /// </summary>
        public static bool IsOutputOf(string fileName, string sourceFilePath, string targetLanguage)
        {
            string stem = Path.GetFileNameWithoutExtension(fileName);
            return string.Equals(stem, GetOriginalBaseName(sourceFilePath), StringComparison.OrdinalIgnoreCase)
                || string.Equals(stem, GetTranslatedBaseName(sourceFilePath, targetLanguage), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Copia las salidas del documento desde la carpeta de salida REAL
        /// de la ejecución (<paramref name="outputDirectory"/>) a
        /// <c>{jobDir}\outputs\</c>. Devuelve la cantidad copiada.
        /// </summary>
        public static int CopyOutputsToJobDirectory(string jobDir, string outputDirectory, string sourceFilePath, string targetLanguage)
        {
            int copiedCount = 0;
            try
            {
                if (!Directory.Exists(outputDirectory)) return 0;

                string outputsDir = Path.Combine(jobDir, "outputs");
                Directory.CreateDirectory(outputsDir);

                foreach (var file in Directory.GetFiles(outputDirectory))
                {
                    string name = Path.GetFileName(file);
                    if (!IsOutputOf(name, sourceFilePath, targetLanguage)) continue;
                    File.Copy(file, Path.Combine(outputsDir, name), true);
                    copiedCount++;
                }
                AppLogger.Info($"Job {Path.GetFileName(jobDir)}: Copied {copiedCount} output files for '{Path.GetFileNameWithoutExtension(sourceFilePath)}' to job outputs folder.");
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Failed to copy output files to job directory: {ex.Message}");
            }
            return copiedCount;
        }
    }
}
