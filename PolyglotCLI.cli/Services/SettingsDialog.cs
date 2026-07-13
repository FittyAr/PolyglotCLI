using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Views;
using Terminal.Gui.Input;
using System.Collections.ObjectModel;
using Terminal.Gui.ViewBase;

namespace PolyglotCLI
{
    public static class SettingsDialog
    {
        public static void Show(IApplication app, AppConfig config)
        {
            var dialog = new Dialog { Title = "Advanced Configuration Settings", Width = 82, Height = 24, BorderStyle = LineStyle.Rounded };

            // Categories list on the left
            var categories = new List<string> { "General", "OCR Process", "Translation", "Revision", "Output Formats" };
            var categoryList = new ListView
            {
                X = 1,
                Y = 1,
                Width = 17,
                Height = Dim.Fill(2)
            };
            categoryList.SetSource(new ObservableCollection<string>(categories));

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
            var viewGeneral = new FrameView { Title = "General Settings", X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
            var lblApi = new Label { Text = "LM Studio API URL:", X = 1, Y = 1 };
            var textApiSetting = new TextField { Text = config.ApiUrl, X = 1, Y = 2, Width = Dim.Fill(2) };

            var btnTestConn = new Button { Text = "Test Connection", X = 1, Y = 4 };
            
            var lblModelCheckTimeout = new Label { Text = "Model Check Timeout (sec):", X = 1, Y = 6 };
            var textModelCheckTimeout = new TextField { Text = config.ModelCheckTimeoutSeconds.ToString(), X = 32, Y = 6, Width = 10 };

            var lblOutputDir = new Label { Text = "Output Directory:", X = 1, Y = 8 };
            var textOutputDirSetting = new TextField { Text = config.OutputDirectory ?? "output", X = 32, Y = 8, Width = 24 };

            var checkDebugSetting = new CheckBox { Text = "Debug Mode (processes first 2 pages)", X = 1, Y = 10, Value = config.Debug ? CheckState.Checked : CheckState.UnChecked };

            viewGeneral.Add(lblApi, textApiSetting, btnTestConn, lblModelCheckTimeout, textModelCheckTimeout, lblOutputDir, textOutputDirSetting, checkDebugSetting);

            // --- 2. OCR Panel ---
            var viewOcr = new FrameView { Title = "OCR Process Settings", X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), Visible = false };
            var lblOcrModel = new Label { Text = "OCR/Vision Model Name:", X = 1, Y = 1 };
            var textOcrModel = new TextField { Text = config.DefaultVisionModel ?? "", X = 1, Y = 2, Width = Dim.Fill(10) };
            var btnOcrModelSel = new Button { Text = "Sel", X = Pos.Right(textOcrModel) + 1, Y = 2 };

            var lblOcrTemp = new Label { Text = "OCR Temperature (0.0-1.0):", X = 1, Y = 4 };
            var textOcrTemp = new TextField { Text = config.OcrTemperature.ToString(System.Globalization.CultureInfo.InvariantCulture), X = 32, Y = 4, Width = 10 };

            var lblOcrTimeout = new Label { Text = "OCR Timeout (sec):", X = 1, Y = 6 };
            var textOcrTimeout = new TextField { Text = config.OcrTimeoutSeconds.ToString(), X = 32, Y = 6, Width = 10 };

            viewOcr.Add(lblOcrModel, textOcrModel, btnOcrModelSel, lblOcrTemp, textOcrTemp, lblOcrTimeout, textOcrTimeout);

            // --- 3. Translation Panel ---
            var viewTranslation = new FrameView { Title = "Translation Process Settings", X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), Visible = false };
            var lblTransModel = new Label { Text = "Translation Model Name:", X = 1, Y = 1 };
            var textTransModel = new TextField { Text = config.DefaultModel ?? "", X = 1, Y = 2, Width = Dim.Fill(10) };
            var btnTransModelSel = new Button { Text = "Sel", X = Pos.Right(textTransModel) + 1, Y = 2 };

            var lblTargetLang = new Label { Text = "Target Language:", X = 1, Y = 4 };
            var textTargetLang = new TextField { Text = config.TargetLanguage ?? "Spanish", X = 32, Y = 4, Width = 24 };

            var lblTransTemp = new Label { Text = "Temperature (0.0-1.0):", X = 1, Y = 6 };
            var textTransTemp = new TextField { Text = config.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture), X = 32, Y = 6, Width = 10 };

            var lblMaxChunk = new Label { Text = "Max Chars per Chunk:", X = 1, Y = 8 };
            var textMaxChunk = new TextField { Text = config.MaxCharactersPerChunk.ToString(), X = 32, Y = 8, Width = 10 };

            var lblChunkOverlap = new Label { Text = "Chunk Overlap (chars):", X = 1, Y = 10 };
            var textChunkOverlap = new TextField { Text = config.ChunkOverlapCharacters.ToString(), X = 32, Y = 10, Width = 10 };

            var checkPreserveFormatSetting = new CheckBox { Text = "Preserve Formatting", X = 1, Y = 12, Value = config.PreserveFormat ? CheckState.Checked : CheckState.UnChecked };

            var lblTransTimeoutSetting = new Label { Text = "Translation Timeout (sec):", X = 1, Y = 14 };
            var textTransTimeoutSetting = new TextField { Text = config.TranslationTimeoutSeconds.ToString(), X = 32, Y = 14, Width = 10 };

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
            var viewRevision = new FrameView { Title = "Revision Process Settings", X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), Visible = false };
            var checkEnableReviewSetting = new CheckBox { Text = "Enable Post-Translation Review", X = 1, Y = 1, Value = config.EnableReview ? CheckState.Checked : CheckState.UnChecked };

            var lblReviewModelSetting = new Label { Text = "Review Model Name:", X = 1, Y = 3 };
            var textReviewModelSetting = new TextField { Text = config.ReviewModel ?? "", X = 1, Y = 4, Width = Dim.Fill(10) };
            var btnReviewModelSel = new Button { Text = "Sel", X = Pos.Right(textReviewModelSetting) + 1, Y = 4 };

            var lblReviewTempSetting = new Label { Text = "Review Temp (0.0-1.0):", X = 1, Y = 6 };
            var textReviewTempSetting = new TextField { Text = config.ReviewTemperature.ToString(System.Globalization.CultureInfo.InvariantCulture), X = 32, Y = 6, Width = 10 };

            var lblReviewTimeoutSetting = new Label { Text = "Review Timeout (sec):", X = 1, Y = 8 };
            var textReviewTimeoutSetting = new TextField { Text = config.ReviewTimeoutSeconds.ToString(), X = 32, Y = 8, Width = 10 };

            viewRevision.Add(checkEnableReviewSetting, lblReviewModelSetting, textReviewModelSetting, btnReviewModelSel, lblReviewTempSetting, textReviewTempSetting, lblReviewTimeoutSetting, textReviewTimeoutSetting);

            // --- 5. Formats Panel ---
            var viewFormats = new FrameView { Title = "Output Formats", X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(), Visible = false };

            var checkMd = new CheckBox { Text = "Always generate Markdown (.md) documents", X = 2, 
                Y = 2, 
                Value = config.SaveMarkdown ? CheckState.Checked : CheckState.UnChecked };

            var lblDefaultFormat = new Label { Text = "Default Additional Output Format:", X = 2, 
                Y = 4 };

            var defaultFormatsList = new List<string> { "None" };
            defaultFormatsList.AddRange(config.SupportedOutputFormats ?? new List<string> { "html", "docx", "odf", "pdf" });
            var comboDefaultFormat = new DropDownList()
            {
                X = 2,
                Y = 5,
                Width = 15,
                Height = 1
            };
            comboDefaultFormat.Source = new ListWrapper<string>(new System.Collections.ObjectModel.ObservableCollection<string>(defaultFormatsList));
            comboDefaultFormat.Text = string.IsNullOrEmpty(config.DefaultOutputFormat) ? "None" : config.DefaultOutputFormat;

            var lblFormatsNotice = new Label { Text = "Note: Formats other than MD are generated as post-process.", X = 2, Y = 12 };

            viewFormats.Add(checkMd, lblDefaultFormat, comboDefaultFormat, lblFormatsNotice);

            // Add all view panels to the container
            rightContainer.Add(viewGeneral, viewOcr, viewTranslation, viewRevision, viewFormats);

            // Wire vertical tabs selection change
            categoryList.ValueChanged += (s, e) =>
            {
                viewGeneral.Visible = (categoryList.SelectedItem ?? -1) == 0;
                viewOcr.Visible = (categoryList.SelectedItem ?? -1) == 1;
                viewTranslation.Visible = (categoryList.SelectedItem ?? -1) == 2;
                viewRevision.Visible = (categoryList.SelectedItem ?? -1) == 3;
                viewFormats.Visible = (categoryList.SelectedItem ?? -1) == 4;
                // Force a full repaint to prevent ghosting/overlap between panels
                dialog.SetNeedsDraw();
            };

            // Model selection click handlers
            btnOcrModelSel.Accepted += (s, e) =>
            {
                ModelSelectionDialog.Show(app, textOcrModel, "OCR", textApiSetting.Text?.ToString()?.Trim() ?? config.ApiUrl, config.ModelCheckTimeoutSeconds);
            };

            btnTransModelSel.Accepted += (s, e) =>
            {
                ModelSelectionDialog.Show(app, textTransModel, "Translation", textApiSetting.Text?.ToString()?.Trim() ?? config.ApiUrl, config.ModelCheckTimeoutSeconds);
            };

            btnReviewModelSel.Accepted += (s, e) =>
            {
                ModelSelectionDialog.Show(app, textReviewModelSetting, "Review", textApiSetting.Text?.ToString()?.Trim() ?? config.ApiUrl, config.ModelCheckTimeoutSeconds);
            };

            // Test Connection click handler
            btnTestConn.Accepted += (s, e) =>
            {
                string testUrl = textApiSetting.Text?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(testUrl))
                {
                    MessageBox.ErrorQuery(app, "Error",  "API URL is empty.", new[] { "OK" });
                    return;
                }

                var dProgress = new Dialog { Title = "Testing Connection", Width = 45, Height = 5, BorderStyle = LineStyle.Rounded };
                var lblStatus = new Label { Text = "Connecting to LM Studio...", X = Pos.Center(), Y = 1 };
                dProgress.Add(lblStatus);

                bool success = false;
                string message = "";

                dProgress.Initialized += (s, e) =>
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            var result = await ModelManagerService.TestApiConnectionAsync(testUrl, 3);
                            success = result.Success;
                            message = result.Message;
                        }
                        catch (Exception ex)
                        {
                            message = $"Connection failed: {ex.Message}";
                        }
                        finally
                        {
                            app.Invoke(() =>
                            {
                                app.RequestStop(dProgress);
                            });
                        }
                    });
                };

                InteractiveMenu.OpenModal();
                app.Run(dProgress);
                InteractiveMenu.CloseModal();

                if (success)
                {
                    MessageBox.Query(app, "Success",  message, new[] { "OK" });
                }
                else
                {
                    MessageBox.ErrorQuery(app, "Connection Failed",  message, new[] { "OK" });
                }
            };

            // Bottom Buttons
            var btnSave = new Button { Text = "Save", IsDefault = true };
            var btnCancelSettings = new Button { Text = "Cancel" };

            btnSave.Accepted += (s, e) =>
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
                    config.UpdateAndSaveSettings(
                        textApiSetting.Text?.ToString()?.Trim() ?? "",
                        checkTimeout,
                        textOutputDirSetting.Text?.ToString()?.Trim() ?? "output",
                        checkDebugSetting.Value == CheckState.Checked,
                        textOcrModel.Text?.ToString(),
                        ocrTemp,
                        ocrTimeout,
                        textTransModel.Text?.ToString(),
                        textTargetLang.Text?.ToString()?.Trim() ?? "Spanish",
                        transTemp,
                        chunkSize,
                        chunkOverlap,
                        checkPreserveFormatSetting.Value == CheckState.Checked,
                        transTimeout,
                        checkEnableReviewSetting.Value == CheckState.Checked,
                        textReviewModelSetting.Text?.ToString(),
                        reviewTemp,
                        reviewTimeout,
                        checkMd.Value == CheckState.Checked,
                        comboDefaultFormat.Text?.ToString()
                    );
                    MessageBox.Query(app, "Success",  "Settings saved successfully!", new[] { "OK" });
                    app.RequestStop(dialog);
                }
                else
                {
                    MessageBox.ErrorQuery(app, "Error",  "Please enter valid numeric values.", new[] { "OK" });
                }
            };

            btnCancelSettings.Accepted += (s, e) =>
            {
                app.RequestStop(dialog);
            };

            dialog.AddButton(btnSave);
            dialog.AddButton(btnCancelSettings);

            InteractiveMenu.OpenModal();
            app.Run(dialog);
            InteractiveMenu.CloseModal();
        }
    }
}
