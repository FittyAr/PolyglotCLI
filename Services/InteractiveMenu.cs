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
                Width = Dim.Percent(35),
                Height = Dim.Fill(2)
            };

            var labelApi = new Label("LM Studio API URL:") { X = 1, Y = 1 };
            var textApi = new TextField(config.ApiUrl) { X = 1, Y = 2, Width = Dim.Fill(2) };

            var labelModel = new Label("Translation Model Name:") { X = 1, Y = 4 };
            var textModel = new TextField(config.DefaultModel ?? "") { X = 1, Y = 5, Width = Dim.Fill(10) };
            var btnSelectModel = new Button("Select") { X = Pos.Right(textModel) + 1, Y = 5 };

            var labelVisionModel = new Label("Vision/OCR Model Name:") { X = 1, Y = 7 };
            var textVisionModel = new TextField(config.DefaultVisionModel ?? "") { X = 1, Y = 8, Width = Dim.Fill(10) };
            var btnSelectVisionModel = new Button("Select") { X = Pos.Right(textVisionModel) + 1, Y = 8 };

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

            var btnSavePresets = new Button("Save Presets") { X = 1, Y = 18 };

            leftFrame.Add(
                labelApi, textApi,
                labelModel, textModel, btnSelectModel,
                labelVisionModel, textVisionModel, btnSelectVisionModel,
                labelLang, textLang,
                labelOutputDir, textOutputDir,
                checkDebug,
                btnSavePresets
            );

            // Right Panel: Document Scanning & Selection
            var rightFrame = new FrameView("Documents Scanner")
            {
                X = Pos.Right(leftFrame),
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(2)
            };

            var labelScanDir = new Label("Directory to Scan:") { X = 1, Y = 1 };
            var textScanDir = new TextField(config.LastScanDirectory ?? ".") { X = 1, Y = 2, Width = Dim.Fill(12) };
            var btnScan = new Button("Scan") { X = Pos.Right(textScanDir) + 1, Y = 2 };

            var labelFiles = new Label("Documents Found (Space/Double-Click to select):") { X = 1, Y = 4 };
            var tableHeader = new Label("Sel | File Name            | Type | Mode                 | Pages    ") { X = 1, Y = 5, ColorScheme = Colors.Base };
            var listFiles = new ListView(new List<string>())
            {
                X = 1,
                Y = 6,
                Width = Dim.Fill(2),
                Height = Dim.Percent(55)
            };

            var labelShortcuts = new Label("Keys: [Space] Toggle Sel | [T] Toggle Mode | [P] Set Pages")
            {
                X = 1,
                Y = Pos.Bottom(listFiles),
                Width = Dim.Fill(2),
                ColorScheme = Colors.Menu
            };

            // Bottom-Right Frame: PDF options (hidden/disabled when not viewing a PDF)
            var pdfOptionsFrame = new FrameView("PDF Options")
            {
                X = 1,
                Y = Pos.Bottom(labelShortcuts) + 1,
                Width = Dim.Fill(2),
                Height = Dim.Fill(1),
                Visible = false
            };

            var labelPdfMode = new Label("OCR Mode:") { X = 1, Y = 0 };
            var radioPdfMode = new RadioGroup(new NStack.ustring[] { "Text Extraction", "Image/OCR" })
            {
                X = 1,
                Y = 1
            };

            var labelPdfPages = new Label("Page Range (e.g. 1-5, all):") { X = 1, Y = 3 };
            var textPdfPages = new TextField("all") { X = 1, Y = 4, Width = Dim.Fill(2) };

            pdfOptionsFrame.Add(labelPdfMode, radioPdfMode, labelPdfPages, textPdfPages);

            rightFrame.Add(
                labelScanDir, textScanDir, btnScan,
                labelFiles, tableHeader, listFiles, labelShortcuts,
                pdfOptionsFrame
            );

            // Bottom Actions
            var btnStart = new Button("Start Translation")
            {
                X = Pos.Center() - 15,
                Y = Pos.AnchorEnd(1)
            };

            var btnCancel = new Button("Quit")
            {
                X = Pos.Center() + 10,
                Y = Pos.AnchorEnd(1)
            };

            win.Add(leftFrame, rightFrame, btnStart, btnCancel);
            Application.Top.Add(win);

            // Add F1 help key shortcut
            win.KeyPress += (View.KeyEventEventArgs args) => {
                if (args.KeyEvent.Key == Key.F1)
                {
                    ShowHelpModal();
                    args.Handled = true;
                }
            };

            // Data Sources
            var filesSource = new List<SelectableFile>();
            int lastSelectedIndex = -1;
            bool updatingPdfOptions = false;

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

            // Helper to save PDF options of previously selected file
            void SaveLastSelectedPdfOptions()
            {
                if (lastSelectedIndex >= 0 && lastSelectedIndex < filesSource.Count)
                {
                    var file = filesSource[lastSelectedIndex];
                    if (Path.GetExtension(file.FullPath).ToLowerInvariant() == ".pdf")
                    {
                        file.Mode = (radioPdfMode.SelectedItem == 1) ? "image" : "text";
                        file.PageRange = textPdfPages.Text?.ToString()?.Trim() ?? "all";
                    }
                }
            }

            // Helper to update PDF options UI panel based on highlighted item
            void UpdatePdfOptionsPanel()
            {
                if (updatingPdfOptions) return;
                updatingPdfOptions = true;

                int idx = listFiles.SelectedItem;
                if (idx >= 0 && idx < filesSource.Count)
                {
                    var file = filesSource[idx];
                    string ext = Path.GetExtension(file.FullPath).ToLowerInvariant();
                    if (ext == ".pdf")
                    {
                        pdfOptionsFrame.Visible = true;
                        radioPdfMode.SelectedItem = file.Mode.Equals("image", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                        textPdfPages.Text = file.PageRange;
                    }
                    else
                    {
                        pdfOptionsFrame.Visible = false;
                    }
                }
                else
                {
                    pdfOptionsFrame.Visible = false;
                }

                updatingPdfOptions = false;
            }

            // Perform directory scanning
            void PerformScan()
            {
                string dirPath = textScanDir.Text?.ToString()?.Trim() ?? ".";
                if (string.IsNullOrEmpty(dirPath))
                {
                    dirPath = ".";
                }

                if (!Directory.Exists(dirPath))
                {
                    MessageBox.ErrorQuery("Directory Not Found", $"The directory '{dirPath}' does not exist.", "OK");
                    return;
                }

                SaveLastSelectedPdfOptions();
                filesSource.Clear();
                lastSelectedIndex = -1;

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
                    UpdatePdfOptionsPanel();

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

            // Help Modal Dialog
            void ShowHelpModal()
            {
                var dialog = new Dialog("Keyboard Shortcuts", 60, 11);
                var content = new Label(
                    "Use the following shortcuts to control the application:\n\n" +
                    "  [Space] / Double-Click : Toggle selection of document\n" +
                    "  [T] / [M]             : Toggle OCR Mode (Text vs Image) for highlighted PDF\n" +
                    "  [P]                   : Set Page Range for highlighted PDF\n" +
                    "  [F1]                  : Show this help dialog\n" +
                    "  [Ctrl+Q]              : Exit application"
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
                    httpClient.Timeout = TimeSpan.FromSeconds(5);
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
            btnScan.Clicked += () => {
                PerformScan();
            };

            btnSelectModel.Clicked += () => {
                ShowModelSelectionModal(textModel, "Translation");
            };

            btnSelectVisionModel.Clicked += () => {
                ShowModelSelectionModal(textVisionModel, "Vision/OCR");
            };

            btnSavePresets.Clicked += () => {
                config.ApiUrl = textApi.Text?.ToString()?.Trim() ?? "";
                config.DefaultModel = textModel.Text?.ToString()?.Trim();
                config.DefaultVisionModel = textVisionModel.Text?.ToString()?.Trim();
                config.TargetLanguage = textLang.Text?.ToString()?.Trim() ?? "Spanish";
                config.OutputDirectory = textOutputDir.Text?.ToString()?.Trim() ?? "output";
                config.LastScanDirectory = textScanDir.Text?.ToString()?.Trim() ?? ".";
                config.Debug = checkDebug.Checked;
                
                config.Save();
                MessageBox.Query("Success", "Presets saved successfully to config.json!", "OK");
            };

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
                        
                        updatingPdfOptions = true;
                        radioPdfMode.SelectedItem = file.Mode.Equals("image", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                        updatingPdfOptions = false;

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
                            
                            updatingPdfOptions = true;
                            textPdfPages.Text = file.PageRange;
                            updatingPdfOptions = false;

                            int savedIndex = listFiles.SelectedItem;
                            UpdateFileList();
                            listFiles.SelectedItem = savedIndex;
                        }
                        args.Handled = true;
                    }
                }
            };

            listFiles.SelectedItemChanged += (ListViewItemEventArgs args) => {
                SaveLastSelectedPdfOptions();
                lastSelectedIndex = listFiles.SelectedItem;
                UpdatePdfOptionsPanel();
            };

            radioPdfMode.SelectedItemChanged += (args) => {
                if (updatingPdfOptions) return;
                int idx = listFiles.SelectedItem;
                if (idx >= 0 && idx < filesSource.Count)
                {
                    var file = filesSource[idx];
                    file.Mode = (args.SelectedItem == 1) ? "image" : "text";
                    
                    int savedIndex = listFiles.SelectedItem;
                    UpdateFileList();
                    listFiles.SelectedItem = savedIndex;
                }
            };

            textPdfPages.TextChanged += (oldText) => {
                if (updatingPdfOptions) return;
                int idx = listFiles.SelectedItem;
                if (idx >= 0 && idx < filesSource.Count)
                {
                    var file = filesSource[idx];
                    file.PageRange = textPdfPages.Text?.ToString()?.Trim() ?? "all";
                    
                    int savedIndex = listFiles.SelectedItem;
                    UpdateFileList();
                    listFiles.SelectedItem = savedIndex;
                }
            };

            btnCancel.Clicked += () => {
                resultOptions = null;
                Application.RequestStop();
            };

            btnStart.Clicked += () => {
                var selected = filesSource.FindAll(f => f.IsSelected);
                if (selected.Count == 0)
                {
                    MessageBox.ErrorQuery("No Files Selected", "You must select at least one file to translate.", "OK");
                    return;
                }

                SaveLastSelectedPdfOptions();

                var finalOptions = new CommandLineOptions
                {
                    ApiUrl = textApi.Text?.ToString()?.Trim() ?? "",
                    ModelName = string.IsNullOrWhiteSpace(textModel.Text?.ToString()) ? null : textModel.Text?.ToString()?.Trim(),
                    VisionModelName = string.IsNullOrWhiteSpace(textVisionModel.Text?.ToString()) ? null : textVisionModel.Text?.ToString()?.Trim(),
                    TargetLanguage = textLang.Text?.ToString()?.Trim() ?? "Spanish",
                    OutputDirectory = textOutputDir.Text?.ToString()?.Trim() ?? "output",
                    Debug = checkDebug.Checked,
                    DocumentTargets = new List<DocumentTarget>()
                };

                foreach (var f in selected)
                {
                    finalOptions.DocumentTargets.Add(new DocumentTarget
                    {
                        FilePath = f.FullPath,
                        Mode = f.Mode,
                        PageRange = f.PageRange
                    });
                    finalOptions.Files.Add(f.FullPath);
                }

                resultOptions = finalOptions;
                Application.RequestStop();
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
