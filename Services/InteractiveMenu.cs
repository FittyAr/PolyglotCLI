using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Terminal.Gui;

namespace PolyglotCLI
{
    public static class InteractiveMenu
    {
        public static Task<CommandLineOptions?> RunAsync(AppConfig config)
        {
            CommandLineOptions? resultOptions = null;
            var filesSource = new List<SelectableFile>();

            Application.Init();

            var win = new Window("PolyglotCLI - Document Local Translator (F1: Help)")
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            // Left Panel: Translation Prompt & Actions
            var leftFrame = new FrameView("Translation Prompt & Actions")
            {
                X = 0,
                Y = 0,
                Width = Dim.Percent(38),
                Height = Dim.Fill(2)
            };

            var labelAddPrompt = new Label("Additional Prompt Guidance:") { X = 1, Y = 1 };
            var textAddPrompt = new SafeTextView()
            {
                X = 1,
                Y = 2,
                Width = Dim.Fill(3),
                Height = Dim.Fill(5), // Leave 5 lines for buttons at the bottom
                WordWrap = true
            };
            textAddPrompt.Text = config.AdditionalPrompt ?? "";

            var btnImprovePrompt = new Button("Improve [F5]") { X = 1, Y = Pos.Bottom(textAddPrompt) + 1 };
            var btnAnalyzeFilePrompt = new Button("Analyze File [F7]") { X = Pos.Right(btnImprovePrompt) + 1, Y = Pos.Bottom(textAddPrompt) + 1 };

            var btnSavePresets = new Button("Save Presets [F4]") { X = 1, Y = Pos.Bottom(btnImprovePrompt) + 1 };
            var btnConfig = new Button("Settings [F8]") { X = Pos.Right(btnSavePresets) + 1, Y = Pos.Bottom(btnImprovePrompt) + 1 };

            leftFrame.Add(
                labelAddPrompt, textAddPrompt,
                btnImprovePrompt, btnAnalyzeFilePrompt,
                btnSavePresets, btnConfig
            );

            var scrollBar = new ScrollBarView(textAddPrompt, true);
            scrollBar.ChangedPosition += () => {
                textAddPrompt.TopRow = scrollBar.Position;
                textAddPrompt.SetNeedsDisplay();
            };
            textAddPrompt.DrawContent += (e) => {
                scrollBar.Size = textAddPrompt.Lines;
                scrollBar.Position = textAddPrompt.TopRow;
                scrollBar.LayoutSubviews();
                scrollBar.SetNeedsDisplay();
            };

            leftFrame.Add(scrollBar);

            // Right Panel: Document Scanning & Selection
            var rightFrame = new FrameView("Documents Scanner")
            {
                X = Pos.Right(leftFrame),
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(2)
            };

            var labelScanDir = new Label("Directory to Scan:") { X = 1, Y = 1 };
            var textScanDir = new TextField(config.LastScanDirectory ?? ".") { X = 1, Y = 2, Width = Dim.Fill(16) };
            var btnScan = new Button("Scan [F6]") { X = Pos.Right(textScanDir) + 1, Y = 2 };

            // Tasks Selection Row
            var labelTasks = new Label("Tasks:") { X = 1, Y = 4 };
            var checkTranscribe = new CheckBox("Transcribe") { X = 8, Y = 4, Checked = true };
            var checkTranslate = new CheckBox("Translate") { X = 23, Y = 4, Checked = true };
            var checkVerify = new CheckBox("Verify") { X = 37, Y = 4, Checked = config.EnableReview };
            var checkGenerate = new CheckBox("Gen Doc:") { X = 48, Y = 4, Checked = !string.IsNullOrEmpty(config.DefaultOutputFormat) };

            var formatsList = new List<string> { "html", "docx", "odf", "pdf" };
            var comboFormat = new ComboBox()
            {
                X = 60,
                Y = 4,
                Width = 10,
                Height = 5
            };
            comboFormat.SetSource(formatsList);
            comboFormat.Text = string.IsNullOrEmpty(config.DefaultOutputFormat) ? "html" : config.DefaultOutputFormat;

            var labelFiles = new Label("Documents Found (Space/Double-Click to select):") { X = 1, Y = 6 };
            var tableHeader = new Label("Sel | File Name            | Type | Mode                 | Pages    ") { X = 1, Y = 7, ColorScheme = Colors.Base };
            var listFiles = new ListView(new List<string>())
            {
                X = 1,
                Y = 8,
                Width = Dim.Fill(2),
                Height = Dim.Fill(2)
            };

            var labelShortcuts = new Label("Keys: [Space] Toggle Sel | [T] Toggle Mode | [P] Set Pages")
            {
                X = 1,
                Y = Pos.Bottom(listFiles),
                Width = Dim.Fill(2),
                ColorScheme = Colors.Menu
            };

            rightFrame.Add(
                labelScanDir, textScanDir, btnScan,
                labelTasks, checkTranscribe, checkTranslate, checkVerify, checkGenerate, comboFormat,
                labelFiles, tableHeader, listFiles, labelShortcuts
            );

            // Bottom Actions
            var btnStart = new Button("Start Translation [F9]")
            {
                X = Pos.Center() - 20,
                Y = Pos.AnchorEnd(1)
            };

            var btnCancel = new Button("Quit [F12]")
            {
                X = Pos.Center() + 15,
                Y = Pos.AnchorEnd(1)
            };

            win.Add(leftFrame, rightFrame, btnStart, btnCancel);
            Application.Top.Add(win);

            // Add key shortcuts for all UI buttons globally via RootKeyEvent
            Application.RootKeyEvent += (KeyEvent keyEvent) => {
                if (keyEvent.Key == Key.F1)
                {
                    ShowHelpModal();
                    return true;
                }
                if (keyEvent.Key == Key.F4)
                {
                    SavePresets();
                    return true;
                }
                if (keyEvent.Key == Key.F5)
                {
                    ImprovePromptWithAi();
                    return true;
                }
                if (keyEvent.Key == Key.F7)
                {
                    AnalyzeFileForPromptWithAi();
                    return true;
                }
                if (keyEvent.Key == Key.F8)
                {
                    SettingsDialog.Show(config);
                    return true;
                }
                if (keyEvent.Key == Key.F6)
                {
                    PerformScan();
                    return true;
                }
                if (keyEvent.Key == Key.F9)
                {
                    StartTranslation();
                    return true;
                }
                if (keyEvent.Key == Key.F12)
                {
                    QuitApp();
                    return true;
                }
                return false;
            };

            // Help Modal Dialog
            void ShowHelpModal()
            {
                var dialog = new Dialog("Keyboard Shortcuts & Help", 65, 28);
                var content = new Label(
                    "Use the following function keys or click the buttons:\n\n" +
                    "  [F1]  : Show this shortcuts help dialog\n" +
                    "  [F4]  : Save current settings as presets\n" +
                    "  [F5]  : Improve prompt with AI\n" +
                    "  [F6]  : Scan documents directory\n" +
                    "  [F7]  : Analyze file to generate prompt\n" +
                    "  [F8]  : Advanced settings (models, server, timeouts, formats)\n" +
                    "  [F9]  : Start translation of selected files\n" +
                    "  [F12] : Quit application\n\n" +
                    "  [Space] / Double-Click : Toggle selection of document\n" +
                    "  [T] / [M]             : Toggle OCR Mode (Text/Image) for PDF\n" +
                    "  [P]                   : Set Page Range for PDF\n\n" +
                    "--- Sobre la Temperatura ---\n" +
                    "  0.0  : Traduccion extremadamente consistente\n" +
                    "  0.1  : Opcion recomendada\n" +
                    "  0.2  : Muy buena\n" +
                    "  0.3  : Puede 'embellecer' frases o cambiar estilo\n" +
                    "  >0.5 : No recomendado para traduccion"
                )
                {
                    X = 1,
                    Y = 1,
                    Width = Dim.Fill(),
                    Height = Dim.Fill()
                };
                
                var btnClose = new Button("Close", is_default: true);
                btnClose.Clicked += () => Application.RequestStop();
                dialog.AddButton(btnClose);
                dialog.Add(content);
                
                Application.Run(dialog);
            }

            // AI Prompt Improver modal flow
            void ImprovePromptWithAi()
            {
                string rawInput = textAddPrompt.Text?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(rawInput))
                {
                    MessageBox.ErrorQuery("Error", "Please write some text in the Additional Prompt box first.", "OK");
                    return;
                }

                string url = config.ApiUrl;
                string model = config.DefaultModel ?? "";
                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(model))
                {
                    MessageBox.ErrorQuery("Error", "LM Studio API URL and Translation Model Name must be configured in settings (press F8) first.", "OK");
                    return;
                }

                var dProgress = new Dialog("AI Prompt Improver", 40, 5);
                var lblStatus = new Label("Connecting to LM Studio...") { X = Pos.Center(), Y = 1 };
                dProgress.Add(lblStatus);
                
                string? improvedResult = null;
                string? errorMessage = null;

                dProgress.Loaded += () => {
                    Task.Run(async () => {
                        try
                        {
                            improvedResult = await PromptHelperService.ImprovePromptAsync(
                                rawInput, 
                                url, 
                                model, 
                                config.PromptImproveTimeoutSeconds, 
                                config.Temperature
                            );
                        }
                        catch (Exception ex)
                        {
                            errorMessage = ex.Message;
                        }
                        finally
                        {
                            Application.MainLoop.Invoke(() => {
                                Application.RequestStop();
                            });
                        }
                    });
                };

                Application.Run(dProgress);

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    MessageBox.ErrorQuery("Error", $"Failed to improve prompt: {errorMessage}", "OK");
                    return;
                }

                if (string.IsNullOrEmpty(improvedResult))
                {
                    MessageBox.ErrorQuery("Error", "No output returned from AI.", "OK");
                    return;
                }

                var dPreview = new Dialog("AI Improved Prompt Preview", 75, 20);
                
                var lblOrig = new Label("Original Prompt:") { X = 1, Y = 1 };
                var textOrig = new SafeTextView()
                {
                    X = 1,
                    Y = 2,
                    Width = Dim.Percent(47),
                    Height = Dim.Fill(2),
                    ReadOnly = true,
                    Text = rawInput,
                    WordWrap = true
                };

                var lblNew = new Label("AI Improved Prompt:") { X = Pos.Right(textOrig) + 2, Y = 1 };
                var textNew = new SafeTextView()
                {
                    X = Pos.Right(textOrig) + 2,
                    Y = 2,
                    Width = Dim.Fill(3),
                    Height = Dim.Fill(2),
                    ReadOnly = true,
                    Text = improvedResult,
                    WordWrap = true
                };

                bool apply = false;
                var btnApply = new Button("Apply Changes", is_default: true);
                var btnDiscard = new Button("Discard");

                btnApply.Clicked += () => {
                    apply = true;
                    Application.RequestStop();
                };

                btnDiscard.Clicked += () => {
                    apply = false;
                    Application.RequestStop();
                };

                dPreview.AddButton(btnApply);
                dPreview.AddButton(btnDiscard);
                
                dPreview.Add(lblOrig, textOrig, lblNew, textNew);

                var scrollBarOrig = new ScrollBarView(textOrig, true);
                scrollBarOrig.ChangedPosition += () => {
                    textOrig.TopRow = scrollBarOrig.Position;
                    textOrig.SetNeedsDisplay();
                };
                textOrig.DrawContent += (e) => {
                    scrollBarOrig.Size = textOrig.Lines;
                    scrollBarOrig.Position = textOrig.TopRow;
                    scrollBarOrig.LayoutSubviews();
                    scrollBarOrig.SetNeedsDisplay();
                };

                var scrollBarNew = new ScrollBarView(textNew, true);
                scrollBarNew.ChangedPosition += () => {
                    textNew.TopRow = scrollBarNew.Position;
                    textNew.SetNeedsDisplay();
                };
                textNew.DrawContent += (e) => {
                    scrollBarNew.Size = textNew.Lines;
                    scrollBarNew.Position = textNew.TopRow;
                    scrollBarNew.LayoutSubviews();
                    scrollBarNew.SetNeedsDisplay();
                };

                dPreview.Add(scrollBarOrig, scrollBarNew);

                Application.Run(dPreview);

                if (apply)
                {
                    textAddPrompt.Text = improvedResult;
                    MessageBox.Query("Success", "Prompt updated successfully!", "OK");
                }
            }

            void AnalyzeFileForPromptWithAi()
            {
                int idx = listFiles.SelectedItem;
                if (idx < 0 || idx >= filesSource.Count)
                {
                    MessageBox.ErrorQuery("Error", "Please select/highlight a file in the documents list to analyze.", "OK");
                    return;
                }
                var file = filesSource[idx];

                string url = config.ApiUrl;
                string model = config.DefaultModel ?? "";
                string visionModel = config.DefaultVisionModel ?? "";
                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(model))
                {
                    MessageBox.ErrorQuery("Error", "LM Studio API URL and Translation Model Name must be configured in settings (press F8) first.", "OK");
                    return;
                }

                var dProgress = new Dialog("AI File Context Prompt Generator", 50, 5);
                var lblStatus = new Label("Preparing to analyze file...") { X = Pos.Center(), Y = 1 };
                dProgress.Add(lblStatus);
                
                string? improvedPromptResult = null;
                string? errorMessage = null;

                dProgress.Loaded += () => {
                    Task.Run(async () => {
                        try
                        {
                            improvedPromptResult = await PromptHelperService.GenerateContextPromptAsync(
                                file.FullPath,
                                file.Mode,
                                file.PageRange,
                                config,
                                url,
                                model,
                                visionModel,
                                (status) => {
                                    Application.MainLoop.Invoke(() => {
                                        lblStatus.Text = status;
                                    });
                                }
                            );
                        }
                        catch (Exception ex)
                        {
                            errorMessage = ex.Message;
                        }
                        finally
                        {
                            Application.MainLoop.Invoke(() => {
                                Application.RequestStop();
                            });
                        }
                    });
                };

                Application.Run(dProgress);

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    MessageBox.ErrorQuery("Error", $"Failed to analyze file: {errorMessage}", "OK");
                    return;
                }

                if (string.IsNullOrEmpty(improvedPromptResult))
                {
                    MessageBox.ErrorQuery("Error", "No output returned from AI.", "OK");
                    return;
                }

                var dPreview = new Dialog("AI File Analysis Result Preview", 75, 20);
                
                var lblNew = new Label("Generated Context-Based Prompt:") { X = 1, Y = 1 };
                var textNew = new SafeTextView()
                {
                    X = 1,
                    Y = 2,
                    Width = Dim.Fill(3),
                    Height = Dim.Fill(2),
                    ReadOnly = true,
                    Text = improvedPromptResult,
                    WordWrap = true
                };

                bool apply = false;
                var btnApply = new Button("Apply to Additional Prompt", is_default: true);
                var btnDiscard = new Button("Discard");

                btnApply.Clicked += () => {
                    apply = true;
                    Application.RequestStop();
                };

                btnDiscard.Clicked += () => {
                    apply = false;
                    Application.RequestStop();
                };

                dPreview.AddButton(btnApply);
                dPreview.AddButton(btnDiscard);
                
                dPreview.Add(lblNew, textNew);

                var scrollBarNew = new ScrollBarView(textNew, true);
                scrollBarNew.ChangedPosition += () => {
                    textNew.TopRow = scrollBarNew.Position;
                    textNew.SetNeedsDisplay();
                };
                textNew.DrawContent += (e) => {
                    scrollBarNew.Size = textNew.Lines;
                    scrollBarNew.Position = textNew.TopRow;
                    scrollBarNew.LayoutSubviews();
                    scrollBarNew.SetNeedsDisplay();
                };

                dPreview.Add(scrollBarNew);

                Application.Run(dPreview);

                if (apply)
                {
                    textAddPrompt.Text = improvedPromptResult;
                    MessageBox.Query("Success", "Additional Prompt updated with file analysis context!", "OK");
                }
            }

            // Helper to update file list view
            void UpdateFileList()
            {
                var displayList = new List<string>();
                foreach (var f in filesSource)
                {
                    displayList.Add(f.ToString());
                }
                listFiles.SetSource(displayList);
            }

            // Perform directory scanning
            void PerformScan()
            {
                string dirPath = textScanDir.Text?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(dirPath))
                {
                    MessageBox.ErrorQuery("Validation Error", "Directory to Scan cannot be empty.", "OK");
                    textScanDir.SetFocus();
                    return;
                }

                if (!Directory.Exists(dirPath))
                {
                    MessageBox.ErrorQuery("Validation Error", $"The directory to scan '{dirPath}' does not exist on the system.", "OK");
                    textScanDir.SetFocus();
                    return;
                }

                filesSource.Clear();

                try
                {
                    var files = Directory.GetFiles(dirPath);
                    var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ".pdf", ".docx", ".doc", ".odt", ".odf", ".txt", ".md", 
                        ".json", ".csv", ".xml", ".html", ".jpg", ".jpeg", ".png", ".bmp", ".tiff"
                    };

                    foreach (var file in files)
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (supportedExtensions.Contains(ext))
                        {
                            filesSource.Add(new SelectableFile
                            {
                                FullPath = Path.GetFullPath(file),
                                IsSelected = false,
                                Mode = "text",
                                PageRange = "all"
                            });
                        }
                    }

                    UpdateFileList();

                    if (filesSource.Count == 0)
                    {
                        MessageBox.Query("Scan Results", "No supported documents found in this directory.", "OK");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery("Scanning Error", ex.Message, "OK");
                }
            }

            // Wire events
            btnScan.Clicked += () => PerformScan();
            btnSavePresets.Clicked += () => SavePresets();
            btnConfig.Clicked += () => SettingsDialog.Show(config);
            btnImprovePrompt.Clicked += () => ImprovePromptWithAi();
            btnAnalyzeFilePrompt.Clicked += () => AnalyzeFileForPromptWithAi();
            btnCancel.Clicked += () => QuitApp();
            btnStart.Clicked += () => StartTranslation();

            bool ValidateInputs(bool checkSelectedFiles)
            {
                // 1. LM Studio API URL
                string apiUrl = config.ApiUrl;
                if (string.IsNullOrEmpty(apiUrl))
                {
                    MessageBox.ErrorQuery("Validation Error", "LM Studio API URL cannot be empty.\nConfigure it in settings [F8].", "OK");
                    return false;
                }
                if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var uriResult) || 
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    MessageBox.ErrorQuery("Validation Error", "LM Studio API URL must be a valid HTTP or HTTPS URL (e.g., http://localhost:1234/v1).\nConfigure it in settings [F8].", "OK");
                    return false;
                }

                // 2. Translation Model Name
                string translationModel = config.DefaultModel ?? "";
                if (string.IsNullOrEmpty(translationModel))
                {
                    MessageBox.ErrorQuery("Validation Error", "Translation Model Name cannot be empty.\nConfigure it in settings [F8].", "OK");
                    return false;
                }

                // 3. Vision/OCR Model Name
                string visionModel = config.DefaultVisionModel ?? "";
                if (string.IsNullOrEmpty(visionModel))
                {
                    bool needsVision = false;
                    if (checkSelectedFiles)
                    {
                        foreach (var f in filesSource)
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
                        MessageBox.ErrorQuery("Validation Error", "Vision/OCR Model Name cannot be empty.\nIt is required to translate images or PDFs in OCR mode.\nConfigure it in settings [F8].", "OK");
                        return false;
                    }
                }

                // 4. Target Language
                string targetLang = config.TargetLanguage ?? "";
                if (string.IsNullOrEmpty(targetLang))
                {
                    MessageBox.ErrorQuery("Validation Error", "Target Language cannot be empty.\nConfigure it in settings [F8].", "OK");
                    return false;
                }
                if (!System.Text.RegularExpressions.Regex.IsMatch(targetLang, @"^[a-zA-Z\s\-]+$"))
                {
                    MessageBox.ErrorQuery("Validation Error", "Target Language must contain only letters, spaces, or hyphens (e.g., 'English' or 'Brazilian-Portuguese').\nConfigure it in settings [F8].", "OK");
                    return false;
                }

                // 5. Output Directory
                string outDir = config.OutputDirectory ?? "";
                if (string.IsNullOrEmpty(outDir))
                {
                    MessageBox.ErrorQuery("Validation Error", "Output Directory cannot be empty.\nConfigure it in settings [F8].", "OK");
                    return false;
                }
                char[] invalidChars = Path.GetInvalidPathChars();
                if (outDir.IndexOfAny(invalidChars) >= 0)
                {
                    MessageBox.ErrorQuery("Validation Error", "Output Directory contains invalid path characters.\nConfigure it in settings [F8].", "OK");
                    return false;
                }

                // 6. Directory to Scan
                string scanDir = textScanDir.Text?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(scanDir))
                {
                    MessageBox.ErrorQuery("Validation Error", "Directory to Scan cannot be empty.", "OK");
                    textScanDir.SetFocus();
                    return false;
                }
                if (!Directory.Exists(scanDir))
                {
                    MessageBox.ErrorQuery("Validation Error", $"The directory to scan '{scanDir}' does not exist on the system.", "OK");
                    textScanDir.SetFocus();
                    return false;
                }

                // 7. Check selected files on Start Translation
                if (checkSelectedFiles)
                {
                    int selectedCount = 0;
                    foreach (var f in filesSource)
                    {
                        if (f.IsSelected) selectedCount++;
                    }

                    if (selectedCount == 0)
                    {
                        MessageBox.ErrorQuery("Validation Error", "No documents have been selected.\nPlease select at least one document from the list using the [Space] key.", "OK");
                        listFiles.SetFocus();
                        return false;
                    }
                }

                return true;
            }

            void SavePresets()
            {
                string scanDir = textScanDir.Text?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(scanDir) || !Directory.Exists(scanDir))
                {
                    MessageBox.ErrorQuery("Validation Error", "Please specify a valid Directory to Scan before saving presets.", "OK");
                    textScanDir.SetFocus();
                    return;
                }

                config.LastScanDirectory = scanDir;
                config.AdditionalPrompt = textAddPrompt.Text?.ToString()?.Trim();
                
                config.Save();
                MessageBox.Query("Success", "Presets saved successfully to config.json!", "OK");
            }

            void StartTranslation()
            {
                if (!ValidateInputs(true))
                {
                    return;
                }

                var selected = filesSource.FindAll(f => f.IsSelected);
                var finalOptions = new CommandLineOptions
                {
                    ApiUrl = config.ApiUrl,
                    ModelName = string.IsNullOrWhiteSpace(config.DefaultModel) ? null : config.DefaultModel.Trim(),
                    VisionModelName = string.IsNullOrWhiteSpace(config.DefaultVisionModel) ? null : config.DefaultVisionModel.Trim(),
                    TargetLanguage = config.TargetLanguage ?? "Spanish",
                    OutputDirectory = config.OutputDirectory ?? "output",
                    Debug = config.Debug,
                    AdditionalPrompt = textAddPrompt.Text?.ToString()?.Trim(),
                    DocumentTargets = new List<DocumentTarget>(),
                    Transcribe = checkTranscribe.Checked,
                    Translate = checkTranslate.Checked,
                    Verify = checkVerify.Checked,
                    GenerateDoc = checkGenerate.Checked,
                    SelectedFormat = checkGenerate.Checked ? comboFormat.Text?.ToString()?.Trim().ToLowerInvariant() : null
                };

                foreach (var f in selected)
                {
                    finalOptions.DocumentTargets.Add(new DocumentTarget
                    {
                        FilePath = f.FullPath,
                        Mode = f.Mode,
                        PageRange = f.PageRange,
                        MaxCharactersPerChunk = config.MaxCharactersPerChunk,
                        ChunkOverlapCharacters = config.ChunkOverlapCharacters
                    });
                    finalOptions.Files.Add(f.FullPath);
                }

                resultOptions = finalOptions;
                Application.RequestStop();
            }

            void QuitApp()
            {
                resultOptions = null;
                Application.RequestStop();
            }

            // Toggle file selection on Space key or Double-Click
            listFiles.OpenSelectedItem += (ListViewItemEventArgs args) => {
                int idx = listFiles.SelectedItem;
                if (idx >= 0 && idx < filesSource.Count)
                {
                    filesSource[idx].IsSelected = !filesSource[idx].IsSelected;
                    int savedIndex = listFiles.SelectedItem;
                    UpdateFileList();
                    listFiles.SelectedItem = savedIndex;
                }
            };

            listFiles.KeyPress += (View.KeyEventEventArgs args) => {
                int idx = listFiles.SelectedItem;
                if (idx < 0 || idx >= filesSource.Count) return;
                var file = filesSource[idx];
                string ext = Path.GetExtension(file.FullPath).ToLowerInvariant();

                if (args.KeyEvent.Key == Key.Space)
                {
                    file.IsSelected = !file.IsSelected;
                    int savedIndex = listFiles.SelectedItem;
                    UpdateFileList();
                    listFiles.SelectedItem = savedIndex;
                    args.Handled = true;
                }
                else if (args.KeyEvent.Key == (Key)'t' || args.KeyEvent.Key == (Key)'T' || args.KeyEvent.Key == (Key)'m' || args.KeyEvent.Key == (Key)'M')
                {
                    if (ext == ".pdf")
                    {
                        file.Mode = file.Mode.Equals("image", StringComparison.OrdinalIgnoreCase) ? "text" : "image";
                        
                        int savedIndex = listFiles.SelectedItem;
                        UpdateFileList();
                        listFiles.SelectedItem = savedIndex;
                        args.Handled = true;
                    }
                }
                else if (args.KeyEvent.Key == (Key)'p' || args.KeyEvent.Key == (Key)'P')
                {
                    if (ext == ".pdf")
                    {
                        string? newPageRange = PromptTextDialog("Page Range", $"Enter page range for {file.DisplayName}:", file.PageRange);
                        if (newPageRange != null)
                        {
                            file.PageRange = newPageRange.Trim();
                            
                            int savedIndex = listFiles.SelectedItem;
                            UpdateFileList();
                            listFiles.SelectedItem = savedIndex;
                        }
                        args.Handled = true;
                    }
                }
            };

            // Perform scan of starting directory on startup if it exists
            if (Directory.Exists(config.LastScanDirectory ?? "."))
            {
                // Delayed scan execution after application begins running
                win.Loaded += () => {
                    PerformScan();
                };
            }

            Application.Run();
            Application.Shutdown();

            return Task.FromResult(resultOptions);
        }

        private static string? PromptTextDialog(string title, string promptText, string defaultValue)
        {
            string? result = null;
            var dialog = new Dialog(title, 50, 7);
            
            var label = new Label(promptText)
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill()
            };
            
            var textInput = new TextField(defaultValue)
            {
                X = 1,
                Y = 2,
                Width = Dim.Fill(2)
            };
            
            var btnOk = new Button("OK", is_default: true);
            var btnCancel = new Button("Cancel");
            
            btnOk.Clicked += () => {
                result = textInput.Text?.ToString() ?? "";
                Application.RequestStop();
            };
            
            btnCancel.Clicked += () => {
                result = null;
                Application.RequestStop();
            };
            
            dialog.AddButton(btnOk);
            dialog.AddButton(btnCancel);
            dialog.Add(label, textInput);
            
            Application.Run(dialog);
            
            return result;
        }
    }

    public class SafeTextView : TextView
    {
        public override bool MouseEvent(MouseEvent ev)
        {
            try
            {
                return base.MouseEvent(ev);
            }
            catch (ArgumentOutOfRangeException)
            {
                return true;
            }
        }
    }
}
