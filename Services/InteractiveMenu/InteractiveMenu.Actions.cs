using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using System.Collections.ObjectModel;

namespace PolyglotCLI
{
    public partial class InteractiveMenu
    {
        // Helper to update file list view
        private void UpdateFileList()
        {
            var displayList = new List<string>();
            foreach (var f in _filesSource)
            {
                displayList.Add(f.ToString());
            }
            _listFiles.SetSource(new ObservableCollection<string>(displayList));
        }

        // Perform directory scanning
        private void PerformScan()
        {
            string dirPath = _textScanDir.Text?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(dirPath))
            {
                MessageBox.ErrorQuery(_app, "Validation Error", "Directory to Scan cannot be empty.", new[] { "OK" });
                _textScanDir.SetFocus();
                return;
            }

            if (!Directory.Exists(dirPath))
            {
                MessageBox.ErrorQuery(_app, "Validation Error", $"The directory to scan '{dirPath}' does not exist on the system.", new[] { "OK" });
                _textScanDir.SetFocus();
                return;
            }

            _filesSource.Clear();

            try
            {
                var files = Directory.GetFiles(dirPath);
                var supportedExtensions = new HashSet<string>(_config.SupportedInputExtensions ?? new List<string>
                {
                    ".pdf", ".docx", ".doc", ".odt", ".odf", ".txt", ".md", 
                    ".json", ".csv", ".xml", ".html", ".jpg", ".jpeg", ".png", ".bmp", ".tiff"
                }, StringComparer.OrdinalIgnoreCase);

                foreach (var file in files)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (supportedExtensions.Contains(ext))
                    {
                        _filesSource.Add(new SelectableFile
                        {
                            FullPath = Path.GetFullPath(file),
                            IsSelected = false,
                            Mode = "text",
                            PageRange = "all"
                        });
                    }
                }

                UpdateFileList();

                if (_filesSource.Count == 0)
                {
                    MessageBox.Query(_app, "Scan Results", "No supported documents found in this directory.", new[] { "OK" });
                }
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery(_app, "Scanning Error", ex.Message, new[] { "OK" });
            }
        }

        private bool ValidateInputs(bool checkSelectedFiles)
        {
            // 1. LM Studio API URL
            string apiUrl = _config.ApiUrl;
            if (string.IsNullOrEmpty(apiUrl))
            {
                MessageBox.ErrorQuery(_app, "Validation Error", "LM Studio API URL cannot be empty.\nConfigure it in settings [F8].", new[] { "OK" });
                return false;
            }
            if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var uriResult) || 
                (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
            {
                MessageBox.ErrorQuery(_app, "Validation Error", "LM Studio API URL must be a valid HTTP or HTTPS URL (e.g., http://localhost:1234/v1).\nConfigure it in settings [F8].", new[] { "OK" });
                return false;
            }

            // 2. Translation Model Name
            string translationModel = _config.DefaultModel ?? "";
            if (string.IsNullOrEmpty(translationModel))
            {
                MessageBox.ErrorQuery(_app, "Validation Error", "Translation Model Name cannot be empty.\nConfigure it in settings [F8].", new[] { "OK" });
                return false;
            }

            // 3. Vision/OCR Model Name
            string visionModel = _config.DefaultVisionModel ?? "";
            if (string.IsNullOrEmpty(visionModel))
            {
                bool needsVision = false;
                if (checkSelectedFiles)
                {
                    foreach (var f in _filesSource)
                    {
                        if (f.IsSelected)
                        {
                            string ext = Path.GetExtension(f.FullPath).ToLowerInvariant();
                            if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".tiff" ||
                                (ext == ".pdf" && f.Mode.Equals("image", StringComparison.OrdinalIgnoreCase)))
                            {
                                needsVision = true;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    needsVision = true;
                }

                if (needsVision)
                {
                    MessageBox.ErrorQuery(_app, "Validation Error", "Vision/OCR Model Name cannot be empty.\nIt is required to translate images or PDFs in OCR mode.\nConfigure it in settings [F8].", new[] { "OK" });
                    return false;
                }
            }

            // 4. Target Language
            string targetLang = _config.TargetLanguage ?? "";
            if (string.IsNullOrEmpty(targetLang))
            {
                MessageBox.ErrorQuery(_app, "Validation Error", "Target Language cannot be empty.\nConfigure it in settings [F8].", new[] { "OK" });
                return false;
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(targetLang, @"^[a-zA-Z\s\-]+$"))
            {
                MessageBox.ErrorQuery(_app, "Validation Error", "Target Language must contain only letters, spaces, or hyphens (e.g., 'English' or 'Brazilian-Portuguese').\nConfigure it in settings [F8].", new[] { "OK" });
                return false;
            }

            // 5. Output Directory
            string outDir = _config.OutputDirectory ?? "";
            if (string.IsNullOrEmpty(outDir))
            {
                MessageBox.ErrorQuery(_app, "Validation Error", "Output Directory cannot be empty.\nConfigure it in settings [F8].", new[] { "OK" });
                return false;
            }
            char[] invalidChars = Path.GetInvalidPathChars();
            if (outDir.IndexOfAny(invalidChars) >= 0)
            {
                MessageBox.ErrorQuery(_app, "Validation Error", "Output Directory contains invalid path characters.\nConfigure it in settings [F8].", new[] { "OK" });
                return false;
            }

            // 6. Directory to Scan
            string scanDir = _textScanDir.Text?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(scanDir))
            {
                MessageBox.ErrorQuery(_app, "Validation Error", "Directory to Scan cannot be empty.", new[] { "OK" });
                _textScanDir.SetFocus();
                return false;
            }
            if (!Directory.Exists(scanDir))
            {
                MessageBox.ErrorQuery(_app, "Validation Error", $"The directory to scan '{scanDir}' does not exist on the system.", new[] { "OK" });
                _textScanDir.SetFocus();
                return false;
            }

            // 7. Check selected files on Start Translation
            if (checkSelectedFiles)
            {
                int selectedCount = 0;
                foreach (var f in _filesSource)
                {
                    if (f.IsSelected) selectedCount++;
                }

                if (selectedCount == 0)
                {
                    MessageBox.ErrorQuery(_app, "Validation Error", "No documents have been selected.\nPlease select at least one document from the list using the [Space] key.", new[] { "OK" });
                    _listFiles.SetFocus();
                    return false;
                }
            }

            return true;
        }

        private void SavePresets()
        {
            string scanDir = _textScanDir.Text?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(scanDir) || !Directory.Exists(scanDir))
            {
                MessageBox.ErrorQuery(_app, "Validation Error", "Please specify a valid Directory to Scan before saving presets.", new[] { "OK" });
                _textScanDir.SetFocus();
                return;
            }

            _config.LastScanDirectory = scanDir;
            _config.AdditionalPrompt = _textAddPrompt.Text?.ToString()?.Trim();
            
            _config.Save();
            MessageBox.Query(_app, "Success", "Presets saved successfully to config.json!", new[] { "OK" });
        }

        private void StartTranslation()
        {
            if (!ValidateInputs(true))
            {
                return;
            }

            var selected = _filesSource.FindAll(f => f.IsSelected);
            var finalOptions = new CommandLineOptions
            {
                ApiUrl = _config.ApiUrl,
                ModelName = string.IsNullOrWhiteSpace(_config.DefaultModel) ? null : _config.DefaultModel.Trim(),
                VisionModelName = string.IsNullOrWhiteSpace(_config.DefaultVisionModel) ? null : _config.DefaultVisionModel.Trim(),
                TargetLanguage = _config.TargetLanguage ?? "Spanish",
                OutputDirectory = _config.OutputDirectory ?? "output",
                Debug = _config.Debug,
                AdditionalPrompt = _textAddPrompt.Text?.ToString()?.Trim(),
                DocumentTargets = new List<DocumentTarget>(),
                Transcribe = _checkTranscribe.Value == CheckState.Checked,
                Translate = _checkTranslate.Value == CheckState.Checked,
                Verify = _checkVerify.Value == CheckState.Checked,
                GenerateDoc = _checkGenerate.Value == CheckState.Checked,
                SelectedFormat = _checkGenerate.Value == CheckState.Checked ? _comboFormat.Text?.ToString()?.Trim().ToLowerInvariant() : null
            };

            foreach (var f in selected)
            {
                finalOptions.DocumentTargets.Add(new DocumentTarget
                {
                    FilePath = f.FullPath,
                    Mode = f.Mode,
                    PageRange = f.PageRange,
                    MaxCharactersPerChunk = _config.MaxCharactersPerChunk,
                    ChunkOverlapCharacters = _config.ChunkOverlapCharacters
                });
                finalOptions.Files.Add(f.FullPath);
            }

            _resultOptions = finalOptions;
            _app?.RequestStop(_win);
        }

        private void QuitApp()
        {
            _resultOptions = null;
            _app?.RequestStop(_win);
        }
    }
}
