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

            // Left Panel: Global Settings
            var leftFrame = new FrameView("Global Settings")
            {
                X = 0,
                Y = 0,
                Width = Dim.Percent(38),
                Height = Dim.Fill(2)
            };

            var labelApi = new Label("LM Studio API URL:") { X = 1, Y = 1 };
            var textApi = new TextField(config.ApiUrl) { X = 1, Y = 2, Width = Dim.Fill(2) };

            var labelModel = new Label("Translation Model Name:") { X = 1, Y = 4 };
            var textModel = new TextField(config.DefaultModel ?? "") { X = 1, Y = 5, Width = Dim.Fill(14) };
            var btnSelectModel = new Button("Sel [F2]") { X = Pos.Right(textModel) + 1, Y = 5 };

            var labelVisionModel = new Label("Vision/OCR Model Name:") { X = 1, Y = 7 };
            var textVisionModel = new TextField(config.DefaultVisionModel ?? "") { X = 1, Y = 8, Width = Dim.Fill(14) };
            var btnSelectVisionModel = new Button("Sel [F3]") { X = Pos.Right(textVisionModel) + 1, Y = 8 };

            var labelLang = new Label("Target Language:") { X = 1, Y = 10 };
            var textLang = new TextField(config.TargetLanguage ?? "Spanish") { X = 1, Y = 11, Width = Dim.Fill(2) };

            var labelOutputDir = new Label("Output Directory:") { X = 1, Y = 13 };
            var textOutputDir = new TextField(config.OutputDirectory ?? "output") { X = 1, Y = 14, Width = Dim.Fill(2) };

            var checkDebug = new CheckBox("Debug Mode (Processes first 2 pages only)")
            {
                X = 1,
                Y = 16,
                Checked = config.Debug
            };

            var btnSavePresets = new Button("Save Presets [F4]") { X = 1, Y = 18 };
            var btnConfig = new Button("Settings [F8]") { X = Pos.Right(btnSavePresets) + 1, Y = 18 };

            var labelAddPrompt = new Label("Additional Prompt:") { X = 1, Y = 20 };
            var textAddPrompt = new TextView()
            {
                X = 1,
                Y = 21,
                Width = Dim.Fill(3),
                Height = Dim.Fill(2),
                WordWrap = true
            };
            textAddPrompt.Text = config.AdditionalPrompt ?? "";

            var btnImprovePrompt = new Button("Improve [F5]") { X = 1, Y = Pos.Bottom(textAddPrompt) };
            var btnAnalyzeFilePrompt = new Button("Analyze File [F7]") { X = Pos.Right(btnImprovePrompt) + 1, Y = Pos.Bottom(textAddPrompt) };

            leftFrame.Add(
                labelApi, textApi,
                labelModel, textModel, btnSelectModel,
                labelVisionModel, textVisionModel, btnSelectVisionModel,
                labelLang, textLang,
                labelOutputDir, textOutputDir,
                checkDebug,
                btnSavePresets, btnConfig,
                labelAddPrompt, textAddPrompt, btnImprovePrompt, btnAnalyzeFilePrompt
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

            var labelFiles = new Label("Documents Found (Space/Double-Click to select):") { X = 1, Y = 4 };
            var tableHeader = new Label("Sel | File Name            | Type | Mode                 | Pages    ") { X = 1, Y = 5, ColorScheme = Colors.Base };
            var listFiles = new ListView(new List<string>())
            {
                X = 1,
                Y = 6,
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
                if (keyEvent.Key == Key.F2)
                {
                    ShowModelSelectionModal(textModel, "Translation");
                    return true;
                }
                if (keyEvent.Key == Key.F3)
                {
                    ShowModelSelectionModal(textVisionModel, "Vision/OCR");
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
                    ShowSettingsModal();
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
                var dialog = new Dialog("Keyboard Shortcuts & Help", 60, 16);
                var content = new Label(
                    "Use the following function keys or click the buttons:\n\n" +
                    "  [F1]  : Show this shortcuts help dialog\n" +
                    "  [F2]  : Select translation model\n" +
                    "  [F3]  : Select vision/OCR model\n" +
                    "  [F4]  : Save current settings as presets\n" +
                    "  [F5]  : Improve prompt with AI\n" +
                    "  [F6]  : Scan documents directory\n" +
                    "  [F7]  : Analyze file to generate prompt\n" +
                    "  [F8]  : Advanced application settings\n" +
                    "  [F9]  : Start translation of selected files\n" +
                    "  [F12] : Quit application\n\n" +
                    "  [Space] / Double-Click : Toggle selection of document\n" +
                    "  [T] / [M]             : Toggle OCR Mode (Text/Image) for PDF\n" +
                    "  [P]                   : Set Page Range for PDF"
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

                string url = textApi.Text?.ToString()?.Trim() ?? "";
                string model = textModel.Text?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(model))
                {
                    MessageBox.ErrorQuery("Error", "API URL and Translation Model Name are required to connect to LM Studio.", "OK");
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
                            string promptTemplate = "";
                            string promptPath = Path.Combine("prompts", "prompt_improver_prompt.md");
                            if (File.Exists(promptPath))
                            {
                                promptTemplate = await File.ReadAllTextAsync(promptPath);
                            }
                            else
                            {
                                promptTemplate = "You are an expert translation prompt engineer. Rewrite the following translation instructions to be highly effective for an LLM. Output only the improved text, no intro, no markdown codeblocks:\n\n{user_input}";
                            }

                            string systemMessage = promptTemplate.Replace("{user_input}", rawInput);

                            using var httpClient = new System.Net.Http.HttpClient();
                            httpClient.Timeout = TimeSpan.FromSeconds(config.PromptImproveTimeoutSeconds);
 
                            var requestBody = new
                            {
                                model = model,
                                messages = new[]
                                {
                                    new { role = "user", content = systemMessage }
                                },
                                temperature = config.Temperature
                            };

                            string jsonString = System.Text.Json.JsonSerializer.Serialize(requestBody);
                            var content = new System.Net.Http.StringContent(jsonString, System.Text.Encoding.UTF8, "application/json");

                            var response = await httpClient.PostAsync($"{url.TrimEnd('/')}/chat/completions", content);
                            if (response.IsSuccessStatusCode)
                            {
                                string responseJson = await response.Content.ReadAsStringAsync();
                                using var doc = System.Text.Json.JsonDocument.Parse(responseJson);
                                var choices = doc.RootElement.GetProperty("choices");
                                if (choices.GetArrayLength() > 0)
                                {
                                    string text = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                                    improvedResult = text.Trim();
                                }
                            }
                            else
                            {
                                errorMessage = $"API returned status code: {response.StatusCode}";
                            }
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
                var textOrig = new TextView()
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
                var textNew = new TextView()
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

                string url = textApi.Text?.ToString()?.Trim() ?? "";
                string model = textModel.Text?.ToString()?.Trim() ?? "";
                string visionModel = textVisionModel.Text?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(model))
                {
                    MessageBox.ErrorQuery("Error", "API URL and Translation Model Name are required.", "OK");
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
                            string ocrPrompt = "You are a precise OCR system. Output the text exactly as it is shown in the image.";
                            try
                            {
                                var loader = new PromptLoader();
                                ocrPrompt = loader.LoadOcrPrompt();
                            }
                            catch {}

                            using var client = new LmStudioClient(url, config.TranslationTimeoutSeconds);
                            var ocrService = new OcrService(client, ocrPrompt, visionModel);
                            var pageRenderer = new PdfPageRenderer();
 
                            var target = new DocumentTarget
                            {
                                FilePath = file.FullPath,
                                Mode = file.Mode,
                                PageRange = file.PageRange,
                                MaxCharactersPerChunk = config.MaxCharactersPerChunk
                            };

                            Application.MainLoop.Invoke(() => {
                                lblStatus.Text = "Extracting text content from file...";
                            });

                            var extractor = new DocumentExtractorFactory().GetExtractor(file.FullPath);
                            var pageStates = await extractor.ExtractTextAsync(file.FullPath, target, ocrService, pageRenderer);

                            var sb = new System.Text.StringBuilder();
                            foreach (var s in pageStates)
                            {
                                if (!string.IsNullOrEmpty(s.OcrText))
                                {
                                    sb.AppendLine(s.OcrText);
                                }
                            }
                            string fileText = sb.ToString().Trim();

                            if (string.IsNullOrEmpty(fileText))
                            {
                                throw new Exception("The file content could not be read or is empty.");
                            }

                            Application.MainLoop.Invoke(() => {
                                lblStatus.Text = "Analyzing context and generating prompt...";
                            });

                            string systemPrompt = "You are an expert translation system prompt engineer. Analyze the following document text content to understand its context, main topic, style, tone, and specific domain terminology. " +
                                                  "Based on this analysis, generate an optimal, detailed set of instructions (in English) that a translation model should follow when translating this document. The instructions should specify: " +
                                                  "1) The context and topic of the text. " +
                                                  "2) The tone, style, and formatting that must be preserved. " +
                                                  "3) Any key domain-specific terminology or guidelines. " +
                                                  "Output ONLY the generated instructions/additional prompt. Do NOT include any introductory or concluding text, and do NOT use markdown code blocks.";

                            if (fileText.Length > 60000)
                            {
                                fileText = fileText.Substring(0, 60000) + "\n\n[TRUNCATED DUE TO SIZE]";
                            }

                            string userMessage = $"Here is the text content of the document to analyze:\n\n{fileText}";

                            var requestBody = new
                            {
                                model = model,
                                messages = new[]
                                {
                                    new { role = "system", content = systemPrompt },
                                    new { role = "user", content = userMessage }
                                },
                                temperature = config.Temperature
                            };
 
                            using var httpClient = new System.Net.Http.HttpClient();
                            httpClient.Timeout = TimeSpan.FromSeconds(config.PromptImproveTimeoutSeconds);

                            string jsonString = System.Text.Json.JsonSerializer.Serialize(requestBody);
                            var content = new System.Net.Http.StringContent(jsonString, System.Text.Encoding.UTF8, "application/json");

                            var response = await httpClient.PostAsync($"{url.TrimEnd('/')}/chat/completions", content);
                            if (response.IsSuccessStatusCode)
                            {
                                string responseJson = await response.Content.ReadAsStringAsync();
                                using var doc = System.Text.Json.JsonDocument.Parse(responseJson);
                                var choices = doc.RootElement.GetProperty("choices");
                                if (choices.GetArrayLength() > 0)
                                {
                                    string text = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                                    improvedPromptResult = text.Trim();
                                }
                            }
                            else
                            {
                                errorMessage = $"API returned status code: {response.StatusCode}";
                            }
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
                var textNew = new TextView()
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

            // (Declaration moved to top of RunAsync)

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

            // (Helpers for obsolete PDF options panel removed)

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

            // (Obsolete ShowHelpModal removed, new version defined above)

            void ShowSettingsModal()
            {
                var dialog = new Dialog("Advanced Settings", 65, 17);

                var lblTransTimeout = new Label("Translation Timeout (sec):") { X = 1, Y = 1 };
                var textTransTimeout = new TextField(config.TranslationTimeoutSeconds.ToString()) { X = 32, Y = 1, Width = 15 };

                var lblPromptTimeout = new Label("AI Improve Timeout (sec):") { X = 1, Y = 3 };
                var textPromptTimeout = new TextField(config.PromptImproveTimeoutSeconds.ToString()) { X = 32, Y = 3, Width = 15 };

                var lblModelCheckTimeout = new Label("Model Check Timeout (sec):") { X = 1, Y = 5 };
                var textModelCheckTimeout = new TextField(config.ModelCheckTimeoutSeconds.ToString()) { X = 32, Y = 5, Width = 15 };

                var lblTemperature = new Label("LLM Temperature (0.0-1.0):") { X = 1, Y = 7 };
                var textTemperature = new TextField(config.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)) { X = 32, Y = 7, Width = 15 };

                var lblChunkSize = new Label("Max Chars per Chunk:") { X = 1, Y = 9 };
                var textChunkSize = new TextField(config.MaxCharactersPerChunk.ToString()) { X = 32, Y = 9, Width = 15 };

                var btnSave = new Button("Save", is_default: true);
                var btnCancelSettings = new Button("Cancel");

                btnSave.Clicked += () => {
                    if (int.TryParse(textTransTimeout.Text?.ToString(), out int transTimeout) &&
                        int.TryParse(textPromptTimeout.Text?.ToString(), out int promptTimeout) &&
                        int.TryParse(textModelCheckTimeout.Text?.ToString(), out int checkTimeout) &&
                        double.TryParse(textTemperature.Text?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double temp) &&
                        int.TryParse(textChunkSize.Text?.ToString(), out int chunkSize))
                    {
                        config.TranslationTimeoutSeconds = transTimeout;
                        config.PromptImproveTimeoutSeconds = promptTimeout;
                        config.ModelCheckTimeoutSeconds = checkTimeout;
                        config.Temperature = temp;
                        config.MaxCharactersPerChunk = chunkSize;
                        config.Save();
                        MessageBox.Query("Success", "Settings saved successfully!", "OK");
                        Application.RequestStop();
                    }
                    else
                    {
                        MessageBox.ErrorQuery("Error", "Please enter valid numeric values.", "OK");
                    }
                };

                btnCancelSettings.Clicked += () => {
                    Application.RequestStop();
                };

                dialog.AddButton(btnSave);
                dialog.AddButton(btnCancelSettings);
                dialog.Add(
                    lblTransTimeout, textTransTimeout,
                    lblPromptTimeout, textPromptTimeout,
                    lblModelCheckTimeout, textModelCheckTimeout,
                    lblTemperature, textTemperature,
                    lblChunkSize, textChunkSize
                );

                Application.Run(dialog);
            }

            // Sync model detection logic via selection modal
            void ShowModelSelectionModal(TextField targetField, string roleName)
            {
                string url = textApi.Text?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(url))
                {
                    MessageBox.ErrorQuery("Error", "LM Studio API URL is empty.", "OK");
                    return;
                }

                var detectedList = new List<string>();
                try
                {
                    using var httpClient = new System.Net.Http.HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(config.ModelCheckTimeoutSeconds);
                    var task = httpClient.GetAsync($"{url.TrimEnd('/')}/models");
                    task.Wait();
                    var modelsResponse = task.Result;
                    
                    if (modelsResponse.IsSuccessStatusCode)
                    {
                        var contentTask = modelsResponse.Content.ReadAsStringAsync();
                        contentTask.Wait();
                        string content = contentTask.Result;
                        
                        using var doc = System.Text.Json.JsonDocument.Parse(content);
                        foreach (var item in doc.RootElement.GetProperty("data").EnumerateArray())
                        {
                            string id = item.GetProperty("id").GetString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(id))
                            {
                                detectedList.Add(id);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery("Connection Error", $"Failed to connect to LM Studio: {ex.Message}", "OK");
                    return;
                }

                if (detectedList.Count == 0)
                {
                    MessageBox.Query("Status", "No active models returned from API.", "OK");
                    return;
                }

                var dialog = new Dialog($"Select {roleName} Model", 60, 15);
                
                var label = new Label($"Available models in LM Studio (Select one):")
                {
                    X = 1,
                    Y = 1,
                    Width = Dim.Fill()
                };
                
                var listModels = new ListView(detectedList)
                {
                    X = 1,
                    Y = 2,
                    Width = Dim.Fill(2),
                    Height = Dim.Fill(2)
                };

                string? selected = null;
                var btnOk = new Button("Select", is_default: true);
                var btnCancel = new Button("Cancel");

                btnOk.Clicked += () => {
                    int sel = listModels.SelectedItem;
                    if (sel >= 0 && sel < detectedList.Count)
                    {
                        selected = detectedList[sel];
                    }
                    Application.RequestStop();
                };

                btnCancel.Clicked += () => {
                    selected = null;
                    Application.RequestStop();
                };

                listModels.OpenSelectedItem += (args) => {
                    int sel = listModels.SelectedItem;
                    if (sel >= 0 && sel < detectedList.Count)
                    {
                        selected = detectedList[sel];
                    }
                    Application.RequestStop();
                };

                dialog.AddButton(btnOk);
                dialog.AddButton(btnCancel);
                dialog.Add(label, listModels);

                Application.Run(dialog);

                if (selected != null)
                {
                    targetField.Text = selected;
                }
            }

            // Wire events
            btnScan.Clicked += () => PerformScan();
            btnSelectModel.Clicked += () => ShowModelSelectionModal(textModel, "Translation");
            btnSelectVisionModel.Clicked += () => ShowModelSelectionModal(textVisionModel, "Vision/OCR");
            btnSavePresets.Clicked += () => SavePresets();
            btnConfig.Clicked += () => ShowSettingsModal();
            btnImprovePrompt.Clicked += () => ImprovePromptWithAi();
            btnAnalyzeFilePrompt.Clicked += () => AnalyzeFileForPromptWithAi();
            btnCancel.Clicked += () => QuitApp();
            btnStart.Clicked += () => StartTranslation();

            bool ValidateInputs(bool checkSelectedFiles)
            {
                // 1. LM Studio API URL
                string apiUrl = textApi.Text?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(apiUrl))
                {
                    MessageBox.ErrorQuery("Validation Error", "LM Studio API URL cannot be empty.", "OK");
                    textApi.SetFocus();
                    return false;
                }
                if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var uriResult) || 
                    (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
                {
                    MessageBox.ErrorQuery("Validation Error", "LM Studio API URL must be a valid HTTP or HTTPS URL (e.g., http://localhost:1234/v1).", "OK");
                    textApi.SetFocus();
                    return false;
                }

                // 2. Translation Model Name
                string translationModel = textModel.Text?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(translationModel))
                {
                    MessageBox.ErrorQuery("Validation Error", "Translation Model Name cannot be empty.\nUse the [Sel] button to fetch available models from LM Studio.", "OK");
                    textModel.SetFocus();
                    return false;
                }

                // 3. Vision/OCR Model Name
                string visionModel = textVisionModel.Text?.ToString()?.Trim() ?? "";
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
                        MessageBox.ErrorQuery("Validation Error", "Vision/OCR Model Name cannot be empty.\nIt is required to translate images or PDFs in OCR mode.", "OK");
                        textVisionModel.SetFocus();
                        return false;
                    }
                }

                // 4. Target Language
                string targetLang = textLang.Text?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(targetLang))
                {
                    MessageBox.ErrorQuery("Validation Error", "Target Language cannot be empty (e.g., 'Spanish', 'French').", "OK");
                    textLang.SetFocus();
                    return false;
                }
                if (!System.Text.RegularExpressions.Regex.IsMatch(targetLang, @"^[a-zA-Z\s\-]+$"))
                {
                    MessageBox.ErrorQuery("Validation Error", "Target Language must contain only letters, spaces, or hyphens (e.g., 'English' or 'Brazilian-Portuguese').", "OK");
                    textLang.SetFocus();
                    return false;
                }

                // 5. Output Directory
                string outDir = textOutputDir.Text?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(outDir))
                {
                    MessageBox.ErrorQuery("Validation Error", "Output Directory cannot be empty.", "OK");
                    textOutputDir.SetFocus();
                    return false;
                }
                char[] invalidChars = Path.GetInvalidPathChars();
                if (outDir.IndexOfAny(invalidChars) >= 0)
                {
                    MessageBox.ErrorQuery("Validation Error", "Output Directory contains invalid path characters.", "OK");
                    textOutputDir.SetFocus();
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
                if (!ValidateInputs(false))
                {
                    return;
                }

                config.ApiUrl = textApi.Text?.ToString()?.Trim() ?? "";
                config.DefaultModel = textModel.Text?.ToString()?.Trim();
                config.DefaultVisionModel = textVisionModel.Text?.ToString()?.Trim();
                config.TargetLanguage = textLang.Text?.ToString()?.Trim() ?? "Spanish";
                config.OutputDirectory = textOutputDir.Text?.ToString()?.Trim() ?? "output";
                config.LastScanDirectory = textScanDir.Text?.ToString()?.Trim() ?? ".";
                config.Debug = checkDebug.Checked;
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
                    ApiUrl = textApi.Text?.ToString()?.Trim() ?? "",
                    ModelName = string.IsNullOrWhiteSpace(textModel.Text?.ToString()) ? null : textModel.Text?.ToString()?.Trim(),
                    VisionModelName = string.IsNullOrWhiteSpace(textVisionModel.Text?.ToString()) ? null : textVisionModel.Text?.ToString()?.Trim(),
                    TargetLanguage = textLang.Text?.ToString()?.Trim() ?? "Spanish",
                    OutputDirectory = textOutputDir.Text?.ToString()?.Trim() ?? "output",
                    Debug = checkDebug.Checked,
                    AdditionalPrompt = textAddPrompt.Text?.ToString()?.Trim(),
                    DocumentTargets = new List<DocumentTarget>()
                };

                foreach (var f in selected)
                {
                    finalOptions.DocumentTargets.Add(new DocumentTarget
                    {
                        FilePath = f.FullPath,
                        Mode = f.Mode,
                        PageRange = f.PageRange,
                        MaxCharactersPerChunk = config.MaxCharactersPerChunk
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

    public class SelectableFile
    {
        public string FullPath { get; set; } = string.Empty;
        public string DisplayName => Path.GetFileName(FullPath);
        public bool IsSelected { get; set; }
        public string Mode { get; set; } = "text";
        public string PageRange { get; set; } = "all";

        public override string ToString()
        {
            string prefix = IsSelected ? "[X]" : "[ ]";
            
            string name = DisplayName;
            if (name.Length > 20)
            {
                name = name.Substring(0, 17) + "...";
            }
            name = name.PadRight(20);

            string ext = Path.GetExtension(FullPath).ToUpperInvariant().TrimStart('.');
            if (ext.Length > 4)
            {
                ext = ext.Substring(0, 4);
            }
            ext = ext.PadRight(4);

            string extLower = Path.GetExtension(FullPath).ToLowerInvariant();
            string mode = "";
            if (extLower == ".pdf")
            {
                mode = Mode.Equals("image", StringComparison.OrdinalIgnoreCase) 
                    ? "[ ] Text [X] Image" 
                    : "[X] Text [ ] Image";
            }
            else
            {
                bool isImage = extLower == ".jpg" || extLower == ".jpeg" || extLower == ".png" || extLower == ".bmp" || extLower == ".tiff";
                mode = isImage ? "          [X] Image" : "[X] Text           ";
            }
            mode = mode.PadRight(20);

            string pages = PageRange;
            if (extLower != ".pdf")
            {
                pages = "-";
            }
            pages = $"[ {pages} ]";
            if (pages.Length > 9)
            {
                pages = pages.Substring(0, 9);
            }
            pages = pages.PadRight(9);

            return $"{prefix} | {name} | {ext} | {mode} | {pages}";
        }
    }
}
