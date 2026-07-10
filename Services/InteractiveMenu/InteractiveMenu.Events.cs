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
            // Global keyboard handler — runs before any focused-view handler.
            // This is necessary because Terminal.Gui.Editor.Editor intercepts character keys
            // (T, M, P, Space) at application level even when a ListView has focus.
            // _app is guaranteed non-null here — WireEvents() is called inside the using(app=...) block.
            AppRequired.Keyboard.KeyDown += (object? sender, Key keyEvent) =>
            {
                // While a modal dialog is open, let all keys pass through to the dialog's controls
                if (IsModalOpen) return;

                // ---- Function-key shortcuts (always active) ----
                if (keyEvent.KeyCode == KeyCode.F1)  { ShowHelpModal();                        keyEvent.Handled = true; return; }
                if (keyEvent.KeyCode == KeyCode.F4)  { SavePresets();                          keyEvent.Handled = true; return; }
                if (keyEvent.KeyCode == KeyCode.F5)  { ImprovePromptWithAi();                  keyEvent.Handled = true; return; }
                if (keyEvent.KeyCode == KeyCode.F6)  { PerformScan();                          keyEvent.Handled = true; return; }
                if (keyEvent.KeyCode == KeyCode.F7)  { AnalyzeFileForPromptWithAi();           keyEvent.Handled = true; return; }
                if (keyEvent.KeyCode == KeyCode.F8)  { SettingsDialog.Show(AppRequired, _config); keyEvent.Handled = true; return; }
                if (keyEvent.KeyCode == KeyCode.F9)  { StartTranslation();                     keyEvent.Handled = true; return; }
                if (keyEvent.KeyCode == KeyCode.F12) { QuitApp();                              keyEvent.Handled = true; return; }

                // ---- ListView navigation keys (only when the file list has focus) ----
                if (_listFiles?.HasFocus != true) return;

                int idx = _listFiles.SelectedItem ?? -1;
                if (idx < 0 || idx >= _filesSource.Count) return;

                var file = _filesSource[idx];
                string ext = Path.GetExtension(file.FullPath).ToLowerInvariant();

                // [Space] — toggle selection
                if (keyEvent.KeyCode == KeyCode.Space)
                {
                    file.IsSelected = !file.IsSelected;
                    int saved = _listFiles.SelectedItem ?? -1;
                    UpdateFileList();
                    _listFiles.SelectedItem = saved;
                    keyEvent.Handled = true;
                    return;
                }

                // [T] / [M] — toggle OCR mode (Text ↔ Image) for PDFs
                if (keyEvent.KeyCode == (KeyCode)'t' || keyEvent.KeyCode == (KeyCode)'T' ||
                    keyEvent.KeyCode == (KeyCode)'m' || keyEvent.KeyCode == (KeyCode)'M')
                {
                    if (ext == ".pdf")
                    {
                        file.Mode = file.Mode.Equals("image", StringComparison.OrdinalIgnoreCase) ? "text" : "image";
                        int saved = _listFiles.SelectedItem ?? -1;
                        UpdateFileList();
                        _listFiles.SelectedItem = saved;
                    }
                    keyEvent.Handled = true;
                    return;
                }

                // [P] — set page range for PDFs
                if (keyEvent.KeyCode == (KeyCode)'p' || keyEvent.KeyCode == (KeyCode)'P')
                {
                    if (ext == ".pdf")
                    {
                        string? newPageRange = PromptTextDialog(
                            "Page Range",
                            $"Enter page range for {file.DisplayName}:",
                            file.PageRange);

                        if (newPageRange != null)
                        {
                            file.PageRange = newPageRange.Trim();
                            int saved = _listFiles.SelectedItem ?? -1;
                            UpdateFileList();
                            _listFiles.SelectedItem = saved;
                        }
                    }
                    keyEvent.Handled = true;
                    return;
                }
            };

            // Wire action buttons — fields are guaranteed non-null after BuildLayout()
            _btnScan!.Accepted             += (s, e) => PerformScan();
            _btnSavePresets!.Accepted      += (s, e) => SavePresets();
            _btnConfig!.Accepted           += (s, e) => SettingsDialog.Show(AppRequired, _config);
            _btnImprovePrompt!.Accepted    += (s, e) => ImprovePromptWithAi();
            _btnAnalyzeFilePrompt!.Accepted += (s, e) => AnalyzeFileForPromptWithAi();
            _btnCancel!.Accepted           += (s, e) => QuitApp();
            _btnStart!.Accepted            += (s, e) => StartTranslation();

            // Double-click / Enter on a list item toggles selection
            _listFiles!.Accepted += (s, e) =>
            {
                int idx = _listFiles.SelectedItem ?? -1;
                if (idx >= 0 && idx < _filesSource.Count)
                {
                    _filesSource[idx].IsSelected = !_filesSource[idx].IsSelected;
                    int saved = _listFiles.SelectedItem ?? -1;
                    UpdateFileList();
                    _listFiles.SelectedItem = saved;
                }
            };
        }
    }
}
