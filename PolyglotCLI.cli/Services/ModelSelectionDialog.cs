using System;
using System.Collections.Generic;
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
    public static class ModelSelectionDialog
    {
        public static void Show(IApplication app, TextField targetField, string roleName, string apiUrl, int checkTimeoutSeconds)
        {
            if (string.IsNullOrEmpty(apiUrl))
            {
                MessageBox.ErrorQuery(app, "Error",  "LM Studio API URL is empty.", new[] { "OK" });
                return;
            }

            var detectedList = new List<string>();
            try
            {
                using var client = new LmStudioClient(apiUrl, checkTimeoutSeconds);
                var task = client.GetAvailableModelsAsync();
                task.Wait();
                detectedList = task.Result;
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery(app, "Connection Error",  $"Failed to connect to LM Studio: {ex.Message}", new[] { "OK" });
                return;
            }

            if (detectedList.Count == 0)
            {
                MessageBox.Query(app, "Status",  "No active models returned from API.", new[] { "OK" });
                return;
            }

            var dialog = new Dialog { Title = $"Select {roleName} Model", Width = 60, Height = 15, BorderStyle = LineStyle.Rounded };
            
            var label = new Label 
            { 
                Text = $"Available models in LM Studio (Select one):", 
                X = 1,
                Y = 1,
                Width = Dim.Fill() 
            };
            
            var listModels = new ListView
            {
                X = 1,
                Y = 2,
                Width = Dim.Fill(2),
                Height = Dim.Fill(3)
            };
            listModels.SetSource(new ObservableCollection<string>(detectedList));

            string? selected = null;
            var btnOk = new Button { Text = "Select", X = 15, Y = 12, IsDefault = true };
            var btnCancel = new Button { Text = "Cancel", X = 32, Y = 12 };

            btnOk.Accepted += (s, e) => {
                int sel = listModels.SelectedItem ?? -1;
                if (sel >= 0 && sel < detectedList.Count)
                {
                    selected = detectedList[sel];
                }
                app.RequestStop(dialog);
            };

            btnCancel.Accepted += (s, e) => {
                selected = null;
                app.RequestStop(dialog);
            };

            listModels.Accepted += (s, e) => {
                int sel = listModels.SelectedItem ?? -1;
                if (sel >= 0 && sel < detectedList.Count)
                {
                    selected = detectedList[sel];
                }
                app.RequestStop(dialog);
            };

            dialog.Add(label, listModels, btnOk, btnCancel);

            InteractiveMenu.OpenModal();
            app.Run(dialog);
            InteractiveMenu.CloseModal();

            if (selected != null)
            {
                targetField.Text = selected;
                targetField.SetNeedsDraw();
                targetField.SuperView?.SetNeedsDraw();
            }
        }
    }
}
