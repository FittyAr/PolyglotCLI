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
        // Help Modal Dialog
        private void ShowHelpModal()
        {
            var dialog = new Dialog 
            { 
                Title = "Keyboard Shortcuts & Help", 
                Width = 65, 
                Height = 28,
                BorderStyle = LineStyle.Rounded
            };

            var content = new Label 
            { 
                Text = "Use the following function keys or click the buttons:\n\n" +
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
                    "  >0.5 : No recomendado para traduccion", 
                X = 1,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill() 
            };
            
            var btnClose = new Button { Text = "Close", IsDefault = true };
            btnClose.Accepted += (s, e) => AppRequired.RequestStop(dialog);
            dialog.AddButton(btnClose);
            dialog.Add(content);
            
            AppRequired.Run(dialog);
            dialog.Dispose();
        }

        // AI Prompt Improver modal flow
        private void ImprovePromptWithAi()
        {
            string rawInput = _textAddPrompt?.Text?.ToString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(rawInput))
            {
                MessageBox.ErrorQuery(AppRequired, "Error", "Please write some text in the Additional Prompt box first.", new[] { "OK" });
                return;
            }

            string url = _config.ApiUrl;
            string model = _config.DefaultModel ?? "";
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(model))
            {
                MessageBox.ErrorQuery(AppRequired, "Error", "LM Studio API URL and Translation Model Name must be configured in settings (press F8) first.", new[] { "OK" });
                return;
            }

            var dProgress = new Dialog { Title = "AI Prompt Improver", Width = 40, Height = 5, BorderStyle = LineStyle.Rounded };
            var lblStatus = new Label { Text = "Connecting to LM Studio...", X = Pos.Center(), Y = 1 };
            dProgress.Add(lblStatus);
            
            string? improvedResult = null;
            string? errorMessage = null;

            dProgress.Initialized += (s, e) => {
                Task.Run(async () => {
                    try
                    {
                        improvedResult = await PromptHelperService.ImprovePromptAsync(
                            rawInput, 
                            url, 
                            model, 
                            _config.PromptImproveTimeoutSeconds, 
                            _config.Temperature
                        );
                    }
                    catch (Exception ex)
                    {
                        errorMessage = ex.Message;
                    }
                    finally
                    {
                        AppRequired.Invoke(() => {
                            AppRequired.RequestStop(dProgress);
                        });
                    }
                });
            };

            AppRequired.Run(dProgress);
            dProgress.Dispose();

            if (!string.IsNullOrEmpty(errorMessage))
            {
                MessageBox.ErrorQuery(AppRequired, "Error", $"Failed to improve prompt: {errorMessage}", new[] { "OK" });
                return;
            }

            if (string.IsNullOrEmpty(improvedResult))
            {
                MessageBox.ErrorQuery(AppRequired, "Error", "No output returned from AI.", new[] { "OK" });
                return;
            }

            var dPreview = new Dialog { Title = "AI Improved Prompt Preview", Width = 75, Height = 20, BorderStyle = LineStyle.Rounded };
            
            var lblOrig = new Label { Text = "Original Prompt:", X = 1, Y = 1 };
            var textOrig = new SafeTextView
            {
                X = 1,
                Y = 2,
                Width = Dim.Percent(47),
                Height = Dim.Fill(2),
                ReadOnly = true,
                Text = rawInput,
                WordWrap = true
            };

            var lblNew = new Label { Text = "AI Improved Prompt:", X = Pos.Right(textOrig) + 2, Y = 1 };
            var textNew = new SafeTextView
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
            var btnApply = new Button { Text = "Apply Changes", IsDefault = true };
            var btnDiscard = new Button { Text = "Discard" };

            btnApply.Accepted += (s, e) => {
                apply = true;
                AppRequired.RequestStop(dPreview);
            };

            btnDiscard.Accepted += (s, e) => {
                apply = false;
                AppRequired.RequestStop(dPreview);
            };

            dPreview.AddButton(btnApply);
            dPreview.AddButton(btnDiscard);
            dPreview.Add(lblOrig, textOrig, lblNew, textNew);

            AppRequired.Run(dPreview);
            dPreview.Dispose();

            if (apply && _textAddPrompt is not null)
            {
                _textAddPrompt.Text = improvedResult;
                MessageBox.Query(AppRequired, "Success", "Prompt updated successfully!", new[] { "OK" });
            }
        }

        private void AnalyzeFileForPromptWithAi()
        {
            int idx = _listFiles?.SelectedItem ?? -1;
            if (idx < 0 || idx >= _filesSource.Count)
            {
                MessageBox.ErrorQuery(AppRequired, "Error", "Please select/highlight a file in the documents list to analyze.", new[] { "OK" });
                return;
            }
            var file = _filesSource[idx];

            string url = _config.ApiUrl;
            string model = _config.DefaultModel ?? "";
            string visionModel = _config.DefaultVisionModel ?? "";
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(model))
            {
                MessageBox.ErrorQuery(AppRequired, "Error", "LM Studio API URL and Translation Model Name must be configured in settings (press F8) first.", new[] { "OK" });
                return;
            }

            var dProgress = new Dialog { Title = "AI File Context Prompt Generator", Width = 50, Height = 5, BorderStyle = LineStyle.Rounded };
            var lblStatus = new Label { Text = "Preparing to analyze file...", X = Pos.Center(), Y = 1 };
            dProgress.Add(lblStatus);
            
            string? improvedPromptResult = null;
            string? errorMessage = null;

            dProgress.Initialized += (s, e) => {
                Task.Run(async () => {
                    try
                    {
                        improvedPromptResult = await PromptHelperService.GenerateContextPromptAsync(
                            file.FullPath,
                            file.Mode,
                            file.PageRange,
                            _config,
                            url,
                            model,
                            visionModel,
                            (status) => {
                                AppRequired.Invoke(() => {
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
                        AppRequired.Invoke(() => {
                            AppRequired.RequestStop(dProgress);
                        });
                    }
                });
            };

            AppRequired.Run(dProgress);
            dProgress.Dispose();

            if (!string.IsNullOrEmpty(errorMessage))
            {
                MessageBox.ErrorQuery(AppRequired, "Error", $"Failed to analyze file: {errorMessage}", new[] { "OK" });
                return;
            }

            if (string.IsNullOrEmpty(improvedPromptResult))
            {
                MessageBox.ErrorQuery(AppRequired, "Error", "No output returned from AI.", new[] { "OK" });
                return;
            }

            var dPreview = new Dialog { Title = "AI File Analysis Result Preview", Width = 75, Height = 20, BorderStyle = LineStyle.Rounded };
            
            var lblNew = new Label { Text = "Generated Context-Based Prompt:", X = 1, Y = 1 };
            var textNew = new SafeTextView
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
            var btnApply = new Button { Text = "Apply to Additional Prompt", IsDefault = true };
            var btnDiscard = new Button { Text = "Discard" };

            btnApply.Accepted += (s, e) => {
                apply = true;
                AppRequired.RequestStop(dPreview);
            };

            btnDiscard.Accepted += (s, e) => {
                apply = false;
                AppRequired.RequestStop(dPreview);
            };

            dPreview.AddButton(btnApply);
            dPreview.AddButton(btnDiscard);
            dPreview.Add(lblNew, textNew);

            AppRequired.Run(dPreview);
            dPreview.Dispose();

            if (apply && _textAddPrompt is not null)
            {
                _textAddPrompt.Text = improvedPromptResult;
                MessageBox.Query(AppRequired, "Success", "Additional Prompt updated with file analysis context!", new[] { "OK" });
            }
        }

        private string? PromptTextDialog(string title, string promptText, string defaultValue)
        {
            string? result = null;
            var dialog = new Dialog { Title = title, Width = 50, Height = 7, BorderStyle = LineStyle.Rounded };
            
            var label = new Label 
            { 
                Text = promptText, 
                X = 1,
                Y = 1,
                Width = Dim.Fill() 
            };
            
            var textInput = new TextField 
            { 
                Text = defaultValue, 
                X = 1,
                Y = 2,
                Width = Dim.Fill(2) 
            };
            
            var btnOk = new Button { Text = "OK", IsDefault = true };
            var btnCancel = new Button { Text = "Cancel" };
            
            btnOk.Accepted += (s, e) => {
                result = textInput.Text?.ToString() ?? "";
                AppRequired.RequestStop(dialog);
            };
            
            btnCancel.Accepted += (s, e) => {
                result = null;
                AppRequired.RequestStop(dialog);
            };
            
            dialog.AddButton(btnOk);
            dialog.AddButton(btnCancel);
            dialog.Add(label, textInput);
            
            AppRequired.Run(dialog);
            dialog.Dispose();
            
            return result;
        }
    }
}
