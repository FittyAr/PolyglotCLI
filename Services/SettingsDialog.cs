using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Terminal.Gui;

namespace PolyglotCLI
{
    public static class SettingsDialog
    {
        public static void Show(AppConfig config)
        {
            var dialog = new Dialog("Advanced Configuration Settings", 82, 24);

            // Categories list on the left
            var categories = new List<string> { "General", "OCR Process", "Translation", "Revision", "Output Formats" };
            var categoryList = new ListView(categories)
            {
                X = 1,
                Y = 1,
                Width = 17,
                Height = Dim.Fill(2)
            };

            // Right container view
            var rightContainer = new View()
            {
                X = 19,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill(2)
            };

            // Add left menu list and right container to dialog
            dialog.Add(categoryList, rightContainer);

            // --- 1. General Panel ---
            var viewGeneral = new FrameView("General Settings")
            {
                X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill()
            };
            var lblApi = new Label("LM Studio API URL:") { X = 1, Y = 1 };
            var textApiSetting = new TextField(config.ApiUrl) { X = 1, Y = 2, Width = Dim.Fill(2) };

            var btnTestConn = new Button("Test Connection") { X = 1, Y = 4 };
            
            var lblModelCheckTimeout = new Label("Model Check Timeout (sec):") { X = 1, Y = 6 };
            var textModelCheckTimeout = new TextField(config.ModelCheckTimeoutSeconds.ToString()) { X = 32, Y = 6, Width = 10 };

            var lblOutputDir = new Label("Output Directory:") { X = 1, Y = 8 };
            var textOutputDirSetting = new TextField(config.OutputDirectory ?? "output") { X = 32, Y = 8, Width = 24 };

            var checkDebugSetting = new CheckBox("Debug Mode (processes first 2 pages)") { X = 1, Y = 10, Checked = config.Debug };

            viewGeneral.Add(lblApi, textApiSetting, btnTestConn, lblModelCheckTimeout, textModelCheckTimeout, lblOutputDir, textOutputDirSetting, checkDebugSetting);

            // --- 2. OCR Panel ---
            var viewOcr = new FrameView("OCR Process Settings")
            {
                X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), Visible = false
            };
            var lblOcrModel = new Label("OCR/Vision Model Name:") { X = 1, Y = 1 };
            var textOcrModel = new TextField(config.DefaultVisionModel ?? "") { X = 1, Y = 2, Width = Dim.Fill(10) };
            var btnOcrModelSel = new Button("Sel") { X = Pos.Right(textOcrModel) + 1, Y = 2 };

            var lblOcrTemp = new Label("OCR Temperature (0.0-1.0):") { X = 1, Y = 4 };
            var textOcrTemp = new TextField(config.OcrTemperature.ToString(System.Globalization.CultureInfo.InvariantCulture)) { X = 32, Y = 4, Width = 10 };

            var lblOcrTimeout = new Label("OCR Timeout (sec):") { X = 1, Y = 6 };
            var textOcrTimeout = new TextField(config.OcrTimeoutSeconds.ToString()) { X = 32, Y = 6, Width = 10 };

            viewOcr.Add(lblOcrModel, textOcrModel, btnOcrModelSel, lblOcrTemp, textOcrTemp, lblOcrTimeout, textOcrTimeout);

            // --- 3. Translation Panel ---
            var viewTranslation = new FrameView("Translation Process Settings")
            {
                X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), Visible = false
            };
            var lblTransModel = new Label("Translation Model Name:") { X = 1, Y = 1 };
            var textTransModel = new TextField(config.DefaultModel ?? "") { X = 1, Y = 2, Width = Dim.Fill(10) };
            var btnTransModelSel = new Button("Sel") { X = Pos.Right(textTransModel) + 1, Y = 2 };

            var lblTargetLang = new Label("Target Language:") { X = 1, Y = 4 };
            var textTargetLang = new TextField(config.TargetLanguage ?? "Spanish") { X = 32, Y = 4, Width = 24 };

            var lblTransTemp = new Label("Temperature (0.0-1.0):") { X = 1, Y = 6 };
            var textTransTemp = new TextField(config.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)) { X = 32, Y = 6, Width = 10 };

            var lblMaxChunk = new Label("Max Chars per Chunk:") { X = 1, Y = 8 };
            var textMaxChunk = new TextField(config.MaxCharactersPerChunk.ToString()) { X = 32, Y = 8, Width = 10 };

            var lblChunkOverlap = new Label("Chunk Overlap (chars):") { X = 1, Y = 10 };
            var textChunkOverlap = new TextField(config.ChunkOverlapCharacters.ToString()) { X = 32, Y = 10, Width = 10 };

            var checkPreserveFormatSetting = new CheckBox("Preserve Formatting") { X = 1, Y = 12, Checked = config.PreserveFormat };

            var lblTransTimeoutSetting = new Label("Translation Timeout (sec):") { X = 1, Y = 14 };
            var textTransTimeoutSetting = new TextField(config.TranslationTimeoutSeconds.ToString()) { X = 32, Y = 14, Width = 10 };

            viewTranslation.Add(
                lblTransModel, textTransModel, btnTransModelSel,
                lblTargetLang, textTargetLang,
                lblTransTemp, textTransTemp,
                lblMaxChunk, textMaxChunk,
                lblChunkOverlap, textChunkOverlap,
                checkPreserveFormatSetting,
                lblTransTimeoutSetting, textTransTimeoutSetting
            );

            // --- 4. Revision Panel ---
            var viewRevision = new FrameView("Revision Process Settings")
            {
                X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), Visible = false
            };
            var checkEnableReviewSetting = new CheckBox("Enable Post-Translation Review") { X = 1, Y = 1, Checked = config.EnableReview };

            var lblReviewModelSetting = new Label("Review Model Name:") { X = 1, Y = 3 };
            var textReviewModelSetting = new TextField(config.ReviewModel ?? "") { X = 1, Y = 4, Width = Dim.Fill(10) };
            var btnReviewModelSel = new Button("Sel") { X = Pos.Right(textReviewModelSetting) + 1, Y = 4 };

            var lblReviewTempSetting = new Label("Review Temp (0.0-1.0):") { X = 1, Y = 6 };
            var textReviewTempSetting = new TextField(config.ReviewTemperature.ToString(System.Globalization.CultureInfo.InvariantCulture)) { X = 32, Y = 6, Width = 10 };

            var lblReviewTimeoutSetting = new Label("Review Timeout (sec):") { X = 1, Y = 8 };
            var textReviewTimeoutSetting = new TextField(config.ReviewTimeoutSeconds.ToString()) { X = 32, Y = 8, Width = 10 };

            viewRevision.Add(checkEnableReviewSetting, lblReviewModelSetting, textReviewModelSetting, btnReviewModelSel, lblReviewTempSetting, textReviewTempSetting, lblReviewTimeoutSetting, textReviewTimeoutSetting);

            // --- 5. Formats Panel ---
            var viewFormats = new FrameView("Output Formats")
            {
                X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), Visible = false
            };

            // Split existing formats to determine check state
            var activeFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(config.OutputFormats))
            {
                foreach (var f in config.OutputFormats.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    activeFormats.Add(f.Trim());
                }
            }

            // Checkboxes
            var checkMd = new CheckBox("Markdown (.md)") { X = 2, Y = 2, Checked = activeFormats.Contains("md") || activeFormats.Contains("markdown") || activeFormats.Count == 0 };
            var checkHtml = new CheckBox("HTML (.html)") { X = 2, Y = 4, Checked = activeFormats.Contains("html") };
            var checkPdf = new CheckBox("PDF (.pdf) [requires pandoc]") { X = 2, Y = 6, Checked = activeFormats.Contains("pdf") };
            var checkDocx = new CheckBox("Word Document (.docx) [requires pandoc]") { X = 2, Y = 8, Checked = activeFormats.Contains("docx") };
            var checkOdt = new CheckBox("OpenDocument Text (.odt) [requires pandoc]") { X = 2, Y = 10, Checked = activeFormats.Contains("odt") };

            var lblFormatsNotice = new Label("Note: Formats other than MD are generated as post-process.") { X = 2, Y = 13 };

            viewFormats.Add(checkMd, checkHtml, checkPdf, checkDocx, checkOdt, lblFormatsNotice);

            // Add all view panels to the container
            rightContainer.Add(viewGeneral, viewOcr, viewTranslation, viewRevision, viewFormats);

            // Wire vertical tabs selection change
            categoryList.SelectedItemChanged += (args) =>
            {
                viewGeneral.Visible = categoryList.SelectedItem == 0;
                viewOcr.Visible = categoryList.SelectedItem == 1;
                viewTranslation.Visible = categoryList.SelectedItem == 2;
                viewRevision.Visible = categoryList.SelectedItem == 3;
                viewFormats.Visible = categoryList.SelectedItem == 4;
            };

            // Model selection click handlers
            btnOcrModelSel.Clicked += () =>
            {
                ModelSelectionDialog.Show(textOcrModel, "OCR", textApiSetting.Text?.ToString()?.Trim() ?? config.ApiUrl, config.ModelCheckTimeoutSeconds);
            };

            btnTransModelSel.Clicked += () =>
            {
                ModelSelectionDialog.Show(textTransModel, "Translation", textApiSetting.Text?.ToString()?.Trim() ?? config.ApiUrl, config.ModelCheckTimeoutSeconds);
            };

            btnReviewModelSel.Clicked += () =>
            {
                ModelSelectionDialog.Show(textReviewModelSetting, "Review", textApiSetting.Text?.ToString()?.Trim() ?? config.ApiUrl, config.ModelCheckTimeoutSeconds);
            };

            // Test Connection click handler
            btnTestConn.Clicked += () =>
            {
                string testUrl = textApiSetting.Text?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(testUrl))
                {
                    MessageBox.ErrorQuery("Error", "API URL is empty.", "OK");
                    return;
                }

                var dProgress = new Dialog("Testing Connection", 45, 5);
                var lblStatus = new Label("Connecting to LM Studio...") { X = Pos.Center(), Y = 1 };
                dProgress.Add(lblStatus);

                bool success = false;
                string message = "";

                dProgress.Loaded += () =>
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            using var httpClient = new System.Net.Http.HttpClient();
                            httpClient.Timeout = TimeSpan.FromSeconds(3);
                            var response = await httpClient.GetAsync($"{testUrl.TrimEnd('/')}/models");
                            if (response.IsSuccessStatusCode)
                            {
                                string content = await response.Content.ReadAsStringAsync();
                                using var doc = System.Text.Json.JsonDocument.Parse(content);
                                int modelCount = doc.RootElement.GetProperty("data").GetArrayLength();
                                success = true;
                                message = $"Connection successful!\nDetected {modelCount} loaded models.";
                            }
                            else
                            {
                                message = $"API returned status code: {response.StatusCode}";
                            }
                        }
                        catch (Exception ex)
                        {
                            message = $"Connection failed: {ex.Message}";
                        }
                        finally
                        {
                            Application.MainLoop.Invoke(() =>
                            {
                                Application.RequestStop();
                            });
                        }
                    });
                };

                Application.Run(dProgress);

                if (success)
                {
                    MessageBox.Query("Success", message, "OK");
                }
                else
                {
                    MessageBox.ErrorQuery("Connection Failed", message, "OK");
                }
            };

            // Bottom Buttons
            var btnSave = new Button("Save", is_default: true);
            var btnCancelSettings = new Button("Cancel");

            btnSave.Clicked += () =>
            {
                if (int.TryParse(textModelCheckTimeout.Text?.ToString(), out int checkTimeout) &&
                    int.TryParse(textOcrTimeout.Text?.ToString(), out int ocrTimeout) &&
                    double.TryParse(textOcrTemp.Text?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double ocrTemp) &&
                    int.TryParse(textTransTimeoutSetting.Text?.ToString(), out int transTimeout) &&
                    double.TryParse(textTransTemp.Text?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double transTemp) &&
                    int.TryParse(textMaxChunk.Text?.ToString(), out int chunkSize) &&
                    int.TryParse(textChunkOverlap.Text?.ToString(), out int chunkOverlap) &&
                    int.TryParse(textReviewTimeoutSetting.Text?.ToString(), out int reviewTimeout) &&
                    double.TryParse(textReviewTempSetting.Text?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double reviewTemp))
                {
                    // Save basic settings
                    config.ApiUrl = textApiSetting.Text?.ToString()?.Trim() ?? "";
                    config.ModelCheckTimeoutSeconds = checkTimeout;
                    config.OutputDirectory = textOutputDirSetting.Text?.ToString()?.Trim() ?? "output";
                    config.Debug = checkDebugSetting.Checked;

                    // OCR process
                    config.DefaultVisionModel = string.IsNullOrWhiteSpace(textOcrModel.Text?.ToString()) ? null : textOcrModel.Text?.ToString()?.Trim();
                    config.OcrTemperature = ocrTemp;
                    config.OcrTimeoutSeconds = ocrTimeout;

                    // Translation process
                    config.DefaultModel = string.IsNullOrWhiteSpace(textTransModel.Text?.ToString()) ? null : textTransModel.Text?.ToString()?.Trim();
                    config.TargetLanguage = textTargetLang.Text?.ToString()?.Trim() ?? "Spanish";
                    config.Temperature = transTemp;
                    config.MaxCharactersPerChunk = chunkSize;
                    config.ChunkOverlapCharacters = chunkOverlap;
                    config.PreserveFormat = checkPreserveFormatSetting.Checked;
                    config.TranslationTimeoutSeconds = transTimeout;

                    // Revision process
                    config.EnableReview = checkEnableReviewSetting.Checked;
                    config.ReviewModel = string.IsNullOrWhiteSpace(textReviewModelSetting.Text?.ToString()) ? null : textReviewModelSetting.Text?.ToString()?.Trim();
                    config.ReviewTemperature = reviewTemp;
                    config.ReviewTimeoutSeconds = reviewTimeout;

                    // Formats
                    var selectedFormats = new List<string>();
                    if (checkMd.Checked) selectedFormats.Add("md");
                    if (checkHtml.Checked) selectedFormats.Add("html");
                    if (checkPdf.Checked) selectedFormats.Add("pdf");
                    if (checkDocx.Checked) selectedFormats.Add("docx");
                    if (checkOdt.Checked) selectedFormats.Add("odt");
                    if (selectedFormats.Count == 0) selectedFormats.Add("md");
                    config.OutputFormats = string.Join(",", selectedFormats);

                    config.Save();
                    MessageBox.Query("Success", "Settings saved successfully!", "OK");
                    Application.RequestStop();
                }
                else
                {
                    MessageBox.ErrorQuery("Error", "Please enter valid numeric values.", "OK");
                }
            };

            btnCancelSettings.Clicked += () =>
            {
                Application.RequestStop();
            };

            dialog.AddButton(btnSave);
            dialog.AddButton(btnCancelSettings);

            Application.Run(dialog);
        }
    }
}
