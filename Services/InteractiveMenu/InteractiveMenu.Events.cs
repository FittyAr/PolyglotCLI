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
        private void WireEvents()
        {
            // Add key shortcuts for all UI buttons globally via KeyDown.
            // _app is guaranteed non-null here — WireEvents() is called inside the using(app = ...) block.
            AppRequired.Keyboard.KeyDown += (object? sender, Key keyEvent) => {
                if (keyEvent.KeyCode == KeyCode.F1)
                {
                    ShowHelpModal();
                    keyEvent.Handled = true;
                    return;
                }
                if (keyEvent.KeyCode == KeyCode.F4)
                {
                    SavePresets();
                    keyEvent.Handled = true;
                    return;
                }
                if (keyEvent.KeyCode == KeyCode.F5)
                {
                    ImprovePromptWithAi();
                    keyEvent.Handled = true;
                    return;
                }
                if (keyEvent.KeyCode == KeyCode.F7)
                {
                    AnalyzeFileForPromptWithAi();
                    keyEvent.Handled = true;
                    return;
                }
                if (keyEvent.KeyCode == KeyCode.F8)
                {
                    SettingsDialog.Show(AppRequired, _config);
                    keyEvent.Handled = true;
                    return;
                }
                if (keyEvent.KeyCode == KeyCode.F6)
                {
                    PerformScan();
                    keyEvent.Handled = true;
                    return;
                }
                if (keyEvent.KeyCode == KeyCode.F9)
                {
                    StartTranslation();
                    keyEvent.Handled = true;
                    return;
                }
                if (keyEvent.KeyCode == KeyCode.F12)
                {
                    QuitApp();
                    keyEvent.Handled = true;
                    return;
                }
            };

            // Wire action buttons — fields are guaranteed non-null after BuildLayout()
            _btnScan!.Accepted += (s, e) => PerformScan();
            _btnSavePresets!.Accepted += (s, e) => SavePresets();
            _btnConfig!.Accepted += (s, e) => SettingsDialog.Show(AppRequired, _config);
            _btnImprovePrompt!.Accepted += (s, e) => ImprovePromptWithAi();
            _btnAnalyzeFilePrompt!.Accepted += (s, e) => AnalyzeFileForPromptWithAi();
            _btnCancel!.Accepted += (s, e) => QuitApp();
            _btnStart!.Accepted += (s, e) => StartTranslation();

            // Toggle file selection on Space key or Double-Click
            _listFiles!.Accepted += (s, e) => {
                int idx = _listFiles.SelectedItem ?? -1;
                if (idx >= 0 && idx < _filesSource.Count)
                {
                    _filesSource[idx].IsSelected = !_filesSource[idx].IsSelected;
                    int savedIndex = _listFiles.SelectedItem ?? -1;
                    UpdateFileList();
                    _listFiles.SelectedItem = savedIndex;
                }
            };

            // Keyboard navigation in ListView
            _listFiles.KeyDown += (object? sender, Key args) => {
                int idx = _listFiles.SelectedItem ?? -1;
                if (idx < 0 || idx >= _filesSource.Count) return;
                var file = _filesSource[idx];
                string ext = Path.GetExtension(file.FullPath).ToLowerInvariant();

                if (args.KeyCode == KeyCode.Space)
                {
                    file.IsSelected = !file.IsSelected;
                    int savedIndex = _listFiles.SelectedItem ?? -1;
                    UpdateFileList();
                    _listFiles.SelectedItem = savedIndex;
                    args.Handled = true;
                }
                else if (args.KeyCode == (KeyCode)'t' || args.KeyCode == (KeyCode)'T' || args.KeyCode == (KeyCode)'m' || args.KeyCode == (KeyCode)'M')
                {
                    if (ext == ".pdf")
                    {
                        file.Mode = file.Mode.Equals("image", StringComparison.OrdinalIgnoreCase) ? "text" : "image";
                        
                        int savedIndex = _listFiles.SelectedItem ?? -1;
                        UpdateFileList();
                        _listFiles.SelectedItem = savedIndex;
                        args.Handled = true;
                    }
                }
                else if (args.KeyCode == (KeyCode)'p' || args.KeyCode == (KeyCode)'P')
                {
                    if (ext == ".pdf")
                    {
                        string? newPageRange = PromptTextDialog("Page Range", $"Enter page range for {file.DisplayName}:", file.PageRange);
                        if (newPageRange != null)
                        {
                            file.PageRange = newPageRange.Trim();
                            
                            int savedIndex = _listFiles.SelectedItem ?? -1;
                            UpdateFileList();
                            _listFiles.SelectedItem = savedIndex;
                        }
                        args.Handled = true;
                    }
                }
            };
        }
    }
}
