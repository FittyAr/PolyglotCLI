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
            _listFiles!.SetSource(new ObservableCollection<string>(displayList));
        }

        // Perform directory scanning
        private void PerformScan()
        {
            string dirPath = _textScanDir!.Text?.ToString()?.Trim() ?? "";
            _filesSource.Clear();

            try
            {
                var files = DocumentDiscoveryService.ScanDirectory(dirPath, _config);
                _filesSource.AddRange(files);

                UpdateFileList();

                if (_filesSource.Count == 0)
                {
                    MessageBox.Query(AppRequired, "Scan Results", "No supported documents found in this directory.", new[] { "OK" });
                }
            }
            catch (ArgumentException)
            {
                MessageBox.ErrorQuery(AppRequired, "Validation Error", "Directory to Scan cannot be empty.", new[] { "OK" });
                _textScanDir.SetFocus();
            }
            catch (DirectoryNotFoundException)
            {
                MessageBox.ErrorQuery(AppRequired, "Validation Error", $"The directory to scan '{dirPath}' does not exist on the system.", new[] { "OK" });
                _textScanDir.SetFocus();
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery(AppRequired, "Scanning Error", ex.Message, new[] { "OK" });
            }
        }

        private bool ValidateInputs(bool checkSelectedFiles)
        {
            string scanDir = _textScanDir!.Text?.ToString()?.Trim() ?? "";
            
            // If checkSelectedFiles is false, we pass an empty list of files because OCR requirements check is not applicable (or we can pass the entire list to be sure)
            var filesToValidate = checkSelectedFiles ? _filesSource : new List<SelectableFile>();
            
            var (isValid, errMsg) = JobValidator.ValidateJobSettings(_config, scanDir);
            if (!isValid && errMsg != null)
            {
                if (errMsg.Contains("Directory to Scan") || errMsg.Contains("directory to scan"))
                {
                    MessageBox.ErrorQuery(AppRequired, "Validation Error", errMsg, new[] { "OK" });
                    _textScanDir.SetFocus();
                }
                else
                {
                    MessageBox.ErrorQuery(AppRequired, "Validation Error", errMsg + "\nConfigure it in settings [F8].", new[] { "OK" });
                }
                return false;
            }

            var (ocrValid, ocrErrMsg) = JobValidator.ValidateOcrModelRequirement(_config, filesToValidate);
            if (!ocrValid && ocrErrMsg != null)
            {
                MessageBox.ErrorQuery(AppRequired, "Validation Error", ocrErrMsg + "\nConfigure it in settings [F8].", new[] { "OK" });
                return false;
            }

            // Check selected files on Start Translation
            if (checkSelectedFiles)
            {
                int selectedCount = 0;
                foreach (var f in _filesSource)
                {
                    if (f.IsSelected) selectedCount++;
                }

                if (selectedCount == 0)
                {
                    MessageBox.ErrorQuery(AppRequired, "Validation Error", "No documents have been selected.\nPlease select at least one document from the list using the [Space] key.", new[] { "OK" });
                    _listFiles!.SetFocus();
                    return false;
                }
            }

            return true;
        }

        private void SavePresets()
        {
            string scanDir = _textScanDir!.Text?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(scanDir) || !Directory.Exists(scanDir))
            {
                MessageBox.ErrorQuery(AppRequired, "Validation Error", "Please specify a valid Directory to Scan before saving presets.", new[] { "OK" });
                _textScanDir.SetFocus();
                return;
            }

            bool generateDoc = _checkGenerate!.Value == CheckState.Checked;
            string selectedFmt = _comboFormat!.Text?.ToString()?.Trim().ToLowerInvariant() ?? "";

            _config.SavePresets(
                scanDir,
                _textAddPrompt?.Text?.ToString()?.Trim(),
                _checkVerify!.Value == CheckState.Checked,
                generateDoc,
                selectedFmt
            );

            MessageBox.Query(AppRequired, "Success", "Presets saved successfully to config.json!", new[] { "OK" });
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
                AdditionalPrompt = _textAddPrompt?.Text?.ToString()?.Trim(),
                DocumentTargets = new List<DocumentTarget>(),
                Transcribe = _checkTranscribe!.Value == CheckState.Checked,
                Translate = _checkTranslate!.Value == CheckState.Checked,
                Verify = _checkVerify!.Value == CheckState.Checked,
                GenerateDoc = _checkGenerate!.Value == CheckState.Checked,
                SelectedFormat = _checkGenerate.Value == CheckState.Checked ? _comboFormat!.Text?.ToString()?.Trim().ToLowerInvariant() : null
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

        private void LoadPastJobs()
        {
            _pastJobs.Clear();
            var loadedJobs = JobManifestService.LoadPastJobs();
            _pastJobs.AddRange(loadedJobs);
        }

        private void UpdateJobsList()
        {
            var displayList = new List<string>();
            foreach (var job in _pastJobs)
            {
                string dateStr = job.JobId;
                if (job.JobId.Length == 15 && job.JobId.Contains('_'))
                {
                    string y = job.JobId.Substring(0, 4);
                    string m = job.JobId.Substring(4, 2);
                    string d = job.JobId.Substring(6, 2);
                    string h = job.JobId.Substring(9, 2);
                    string min = job.JobId.Substring(11, 2);
                    string s = job.JobId.Substring(13, 2);
                    dateStr = $"{y}-{m}-{d} {h}:{min}:{s}";
                }
                
                string modeStr = job.Mode.ToUpperInvariant();
                string statusStr = job.Status;
                
                displayList.Add($"{dateStr} | {modeStr.PadRight(5)} | {statusStr}");
            }
            
            _listJobs!.SetSource(new ObservableCollection<string>(displayList));
            
            if (_pastJobs.Count == 0)
            {
                _textJobDetails!.Text = "No past jobs found.";
            }
            else
            {
                _listJobs.SelectedItem = 0;
                ShowSelectedJobDetails();
            }
        }

        private void ShowSelectedJobDetails()
        {
            int idx = _listJobs!.SelectedItem ?? -1;
            if (idx < 0 || idx >= _pastJobs.Count)
            {
                _textJobDetails!.Text = "";
                return;
            }

            var job = _pastJobs[idx];
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Job ID: {job.JobId}");
            sb.AppendLine($"Created At: {job.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Last Update: {job.LastUpdatedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Status: {job.Status}");
            sb.AppendLine($"Target Language: {job.TargetLanguage}");
            sb.AppendLine($"OCR/Vision Model: {job.VisionModelName ?? "None"}");
            sb.AppendLine($"Translation Model: {job.ModelName ?? "None"}");
            sb.AppendLine($"Mode: {job.Mode}");
            sb.AppendLine($"Page Range: {job.PageRange}");
            sb.AppendLine($"Tasks: Transcribe={job.Transcribe}, Translate={job.Translate}, Verify={job.Verify}, GenerateDoc={job.GenerateDoc} ({job.SelectedFormat ?? "None"})");
            if (!string.IsNullOrEmpty(job.AdditionalPrompt))
            {
                sb.AppendLine("Additional Prompt Guidance:");
                sb.AppendLine($"  {job.AdditionalPrompt}");
            }
            sb.AppendLine();
            sb.AppendLine("Files & Pages Progress:");
            foreach (var f in job.Files)
            {
                sb.AppendLine($" - {f.OriginalFileName} (Completed: {f.Completed})");
                foreach (var p in f.Pages)
                {
                    var steps = new List<string>();
                    if (job.Transcribe) steps.Add($"OCR: {(p.OcrCompleted ? "OK" : (string.IsNullOrEmpty(p.OcrError) ? "Pending" : $"FAILED ({p.OcrError})"))}");
                    if (job.Translate) steps.Add($"Trans: {(p.TranslationCompleted ? "OK" : (string.IsNullOrEmpty(p.TranslationError) ? "Pending" : $"FAILED ({p.TranslationError})"))}");
                    if (job.Verify) steps.Add($"Review: {(p.ReviewCompleted ? "OK" : (string.IsNullOrEmpty(p.ReviewError) ? "Pending" : $"FAILED ({p.ReviewError})"))}");
                    if (job.GenerateDoc) steps.Add($"Conv: {(p.ConversionCompleted ? "OK" : "Pending")}");
                    
                    sb.AppendLine($"    Page {p.PageNumber}: {string.Join(" | ", steps)}");
                }
            }

            _textJobDetails!.Text = sb.ToString();
        }

        private void ResumeSelectedJob()
        {
            int idx = _listJobs!.SelectedItem ?? -1;
            if (idx < 0 || idx >= _pastJobs.Count)
            {
                MessageBox.ErrorQuery(AppRequired, "Validation Error", "No job selected.", new[] { "OK" });
                return;
            }

            var job = _pastJobs[idx];
            if (job.Status == "Completed")
            {
                var queryResult = MessageBox.Query(AppRequired, "Job Completed", "This job has already completed successfully.\nAre you sure you want to run it again?", new[] { "Yes", "No" });
                if (queryResult != 0)
                {
                    return;
                }
            }

            // Create command line options to resume this job
            _resultOptions = new CommandLineOptions
            {
                ResumeJobId = job.JobId,
                ApiUrl = _config.ApiUrl // Required for verification, but will be overwritten by manifest
            };

            _app?.RequestStop(_win);
        }

        private void ViewSelectedJob()
        {
            int idx = _listJobs!.SelectedItem ?? -1;
            if (idx < 0 || idx >= _pastJobs.Count)
            {
                MessageBox.ErrorQuery(AppRequired, "Validation Error", "No job selected.", new[] { "OK" });
                return;
            }

            var job = _pastJobs[idx];
            string jobDir = Path.Combine(TranslationOrchestrator.GetJobsDirectory(), job.JobId);
            if (!Directory.Exists(jobDir))
            {
                MessageBox.ErrorQuery(AppRequired, "Error", $"Job directory not found:\n{jobDir}", new[] { "OK" });
                return;
            }

            var dialog = new JobViewerDialog(jobDir, _config, AppRequired);
            InteractiveMenu.OpenModal();
            AppRequired.Run(dialog);
            InteractiveMenu.CloseModal();
            dialog.Dispose();
        }
    }
}
