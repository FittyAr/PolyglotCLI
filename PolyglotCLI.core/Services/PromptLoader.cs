using System;
using System.IO;

namespace PolyglotCLI
{
    public class PromptLoader
    {
        private readonly string _promptsDirectory;

        public PromptLoader(string? promptsDirectory = null)
        {
            _promptsDirectory = ResolvePromptsDirectory(promptsDirectory);
        }

        public string PromptsDirectory => _promptsDirectory;

        private static readonly object BootstrapLock = new();
        private static bool _bootstrapDone;

        private static string ResolvePromptsDirectory(string? explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath) && Directory.Exists(explicitPath))
            {
                return explicitPath;
            }

            string? userPrompts = GetUserPromptsDirectory();
            string? bundlePrompts = FindBundledPromptsDirectory();

            // 1) Bootstrap NO destructivo desde el bundle hacia la carpeta
            //    del usuario (%AppData%\FittyAr\PolyglotCLI\prompts\).
            //    Corre siempre (una vez por proceso), no sólo en la primera
            //    ejecución: así los prompts que agrega una versión nueva
            //    (p.ej. error_analysis_prompt.md) llegan también a usuarios
            //    existentes. Nunca pisa archivos que el usuario ya tiene,
            //    por lo que sus ediciones se preservan.
            bool bootstrapOk = false;
            if (!string.IsNullOrEmpty(bundlePrompts) && Directory.Exists(bundlePrompts))
            {
                bootstrapOk = EnsureUserPromptsBootstrapped(userPrompts, bundlePrompts);
            }

            // 2) Ubicación canónica del usuario si ya tiene los prompts.
            if (!string.IsNullOrEmpty(userPrompts)
                && Directory.Exists(userPrompts)
                && File.Exists(Path.Combine(userPrompts, "translation_prompt.md")))
            {
                return userPrompts;
            }

            // 3) Si la copia falló (permisos, etc.) usamos el bundle en
            //    modo lectura. Las ediciones se perderán al actualizar.
            if (!bootstrapOk && !string.IsNullOrEmpty(bundlePrompts) && Directory.Exists(bundlePrompts))
            {
                return bundlePrompts;
            }

            // 4) Fallback: ubicación del usuario aunque esté vacía
            //    (la app igualmente puede crearla al guardar).
            if (!string.IsNullOrEmpty(userPrompts))
            {
                Directory.CreateDirectory(userPrompts);
                return userPrompts;
            }

            // 5) Último recurso: BaseDir/prompts (aunque no exista).
            return Path.Combine(AppContext.BaseDirectory, "prompts");
        }

        /// <summary>
        /// Ejecuta <see cref="TryBootstrapUserPromptsFromBundle"/> una sola
        /// vez por proceso (PromptLoader se instancia muchas veces).
        /// </summary>
        private static bool EnsureUserPromptsBootstrapped(string? userPrompts, string bundlePrompts)
        {
            lock (BootstrapLock)
            {
                if (_bootstrapDone)
                {
                    return !string.IsNullOrEmpty(userPrompts)
                        && File.Exists(Path.Combine(userPrompts, "translation_prompt.md"));
                }
                bool ok = TryBootstrapUserPromptsFromBundle(userPrompts, bundlePrompts);
                _bootstrapDone = ok;
                return ok;
            }
        }

        /// <summary>
        /// Devuelve la ruta canónica de prompts del usuario
        /// (%AppData%\FittyAr\PolyglotCLI\prompts\) en Windows, o
        /// una ruta equivalente fuera de Windows. Vacío si la
        /// plataforma no es Windows (la lógica de base dir cubre
        /// esos casos en el caller).
        /// </summary>
        private static string? GetUserPromptsDirectory()
        {
            if (!OperatingSystem.IsWindows()) return null;
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "FittyAr", "PolyglotCLI", "prompts");
        }

        /// <summary>
        /// Copia los prompts del bundle (assets/prompts o /prompts
        /// relativo al binario) a la ubicación del usuario. No
        /// pisa archivos que ya existan (preserva ediciones del
        /// usuario) y reporta fallos por excepción para que el
        /// caller decida el fallback.
        /// </summary>
        private static bool TryBootstrapUserPromptsFromBundle(string? destDir, string bundleDir)
        {
            if (string.IsNullOrEmpty(destDir)) return false;
            try
            {
                Directory.CreateDirectory(destDir);
                foreach (var srcFile in Directory.GetFiles(bundleDir, "*.md"))
                {
                    string destFile = Path.Combine(destDir, Path.GetFileName(srcFile));
                    if (!File.Exists(destFile))
                    {
                        File.Copy(srcFile, destFile, overwrite: false);
                    }
                }
                return File.Exists(Path.Combine(destDir, "translation_prompt.md"));
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"PromptLoader: no se pudo inicializar {destDir} desde el bundle: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Busca el bundle de prompts que viene con la app
        /// (assets/prompts o prompts, relativo a BaseDir /
        /// CurrentDir o subiendo por el árbol de directorios).
        /// </summary>
        private static string? FindBundledPromptsDirectory()
        {
            string[] candidateRelativePaths = new[] { "assets/prompts", "prompts" };

            // BaseDir
            foreach (var rel in candidateRelativePaths)
            {
                string candidate = Path.Combine(AppContext.BaseDirectory, rel);
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "translation_prompt.md")))
                {
                    return candidate;
                }
            }

            // CurrentDir
            foreach (var rel in candidateRelativePaths)
            {
                string candidate = Path.Combine(Directory.GetCurrentDirectory(), rel);
                if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "translation_prompt.md")))
                {
                    return candidate;
                }
            }

            // Walk up desde CurrentDir
            var searchDir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (searchDir != null)
            {
                foreach (var rel in candidateRelativePaths)
                {
                    string candidate = Path.Combine(searchDir.FullName, rel);
                    if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "translation_prompt.md")))
                    {
                        return candidate;
                    }
                }
                searchDir = searchDir.Parent;
            }

            // Walk up desde BaseDir
            var baseSearchDir = new DirectoryInfo(AppContext.BaseDirectory);
            while (baseSearchDir != null)
            {
                foreach (var rel in candidateRelativePaths)
                {
                    string candidate = Path.Combine(baseSearchDir.FullName, rel);
                    if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "translation_prompt.md")))
                    {
                        return candidate;
                    }
                }
                baseSearchDir = baseSearchDir.Parent;
            }

            return null;
        }

        public string LoadOcrPrompt()
        {
            return LoadPromptFile("ocr_prompt.md");
        }

        public string LoadTranslationPrompt()
        {
            return LoadPromptFile("translation_prompt.md");
        }

        public string LoadReviewPrompt()
        {
            return LoadPromptFile("review_prompt.md");
        }

        public string LoadErrorAnalysisPrompt()
        {
            return LoadPromptFile("error_analysis_prompt.md");
        }

        public string LoadPromptImproverPrompt()
        {
            return LoadPromptFile("prompt_improver_prompt.md");
        }

        public void SaveOcrPrompt(string content)
        {
            SavePromptFile("ocr_prompt.md", content);
        }

        public void SaveTranslationPrompt(string content)
        {
            SavePromptFile("translation_prompt.md", content);
        }

        public void SaveReviewPrompt(string content)
        {
            SavePromptFile("review_prompt.md", content);
        }

        public void SavePromptImproverPrompt(string content)
        {
            SavePromptFile("prompt_improver_prompt.md", content);
        }

        public void SaveErrorAnalysisPrompt(string content)
        {
            SavePromptFile("error_analysis_prompt.md", content);
        }

        private string LoadPromptFile(string filename)
        {
            string fullPath = Path.Combine(_promptsDirectory, filename);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Required prompt file '{filename}' was not found in directory '{_promptsDirectory}'. Make sure you run the app from the root directory or copy prompts to the output directory.");
            }

            return File.ReadAllText(fullPath).Trim();
        }

        private void SavePromptFile(string filename, string content)
        {
            if (!Directory.Exists(_promptsDirectory))
            {
                Directory.CreateDirectory(_promptsDirectory);
            }
            string fullPath = Path.Combine(_promptsDirectory, filename);
            File.WriteAllText(fullPath, content ?? string.Empty, System.Text.Encoding.UTF8);
        }
    }
}
