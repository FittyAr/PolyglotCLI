using System;
using System.Collections.Generic;
using Terminal.Gui;

namespace PolyglotCLI
{
    public static class ModelSelectionDialog
    {
        public static void Show(TextField targetField, string roleName, string apiUrl, int checkTimeoutSeconds)
        {
            if (string.IsNullOrEmpty(apiUrl))
            {
                MessageBox.ErrorQuery("Error", "LM Studio API URL is empty.", "OK");
                return;
            }

            var detectedList = new List<string>();
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(checkTimeoutSeconds);
                var task = httpClient.GetAsync($"{apiUrl.TrimEnd('/')}/models");
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
    }
}
