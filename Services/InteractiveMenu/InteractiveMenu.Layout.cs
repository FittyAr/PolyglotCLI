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
        private void BuildLayout()
        {
            _win = new Window 
            { 
                Title = "PolyglotCLI - Document Local Translator (F1: Help)", 
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill() 
            };

            // Left Tab menu list
            _tabList = new ListView
            {
                X = 0,
                Y = 0,
                Width = 16,
                Height = Dim.Fill(),
                BorderStyle = LineStyle.Single
            };
            _tabList.SetSource(new ObservableCollection<string>(new List<string> { " Translator", " Jobs History" }));

            // Right container view
            _tabContainer = new View()
            {
                X = 17,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            // --- 1. Translator Tab Panel ---
            _translatorView = new View()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            // Left Panel: Translation Prompt & Actions
            _leftFrame = new FrameView 
            { 
                Title = "Translation Prompt & Actions", 
                X = 0,
                Y = 0,
                Width = Dim.Percent(38),
                Height = Dim.Fill(2),
                BorderStyle = LineStyle.Rounded
            };

            var labelAddPrompt = new Label 
            { 
                Text = "Additional Prompt Guidance:", 
                X = 1, 
                Y = 1 
            };

            _textAddPrompt = new SafeTextView
            {
                X = 1,
                Y = 2,
                Width = Dim.Fill(3),
                Height = Dim.Fill(5), // Leave 5 lines for buttons at the bottom
                WordWrap = true
            };
            _textAddPrompt.Text = _config.AdditionalPrompt ?? "";

            _btnImprovePrompt = new Button 
            { 
                Text = "Improve [F5]", 
                X = 1, 
                Y = Pos.Bottom(_textAddPrompt) + 1 
            };

            _btnAnalyzeFilePrompt = new Button 
            { 
                Text = "Analyze File [F7]", 
                X = Pos.Right(_btnImprovePrompt) + 1, 
                Y = Pos.Bottom(_textAddPrompt) + 1 
            };

            _btnSavePresets = new Button 
            { 
                Text = "Save Presets [F4]", 
                X = 1, 
                Y = Pos.Bottom(_btnImprovePrompt) + 1 
            };

            _btnConfig = new Button 
            { 
                Text = "Settings [F8]", 
                X = Pos.Right(_btnSavePresets) + 1, 
                Y = Pos.Bottom(_btnImprovePrompt) + 1 
            };

            _leftFrame.Add(
                labelAddPrompt, _textAddPrompt,
                _btnImprovePrompt, _btnAnalyzeFilePrompt,
                _btnSavePresets, _btnConfig
            );

            // Right Panel: Document Scanning & Selection
            _rightFrame = new FrameView 
            { 
                Title = "Documents Scanner", 
                X = Pos.Right(_leftFrame),
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(2),
                BorderStyle = LineStyle.Rounded
            };

            _labelScanDir = new Label 
            { 
                Text = "Directory to Scan:", 
                X = 1, 
                Y = 1 
            };

            _textScanDir = new TextField 
            { 
                Text = _config.LastScanDirectory ?? ".", 
                X = 1, 
                Y = 2, 
                Width = Dim.Fill(16) 
            };

            _btnScan = new Button 
            { 
                Text = "Scan [F6]", 
                X = Pos.Right(_textScanDir) + 1, 
                Y = 2 
            };

            // Tasks Selection Row
            _labelTasks = new Label 
            { 
                Text = "Tasks:", 
                X = 1, 
                Y = 4 
            };

            _checkTranscribe = new CheckBox 
            { 
                Text = "Transcribe", 
                X = 8, 
                Y = 4, 
                Value = CheckState.Checked 
            };

            _checkTranslate = new CheckBox 
            { 
                Text = "Translate", 
                X = 23, 
                Y = 4, 
                Value = CheckState.Checked 
            };

            _checkVerify = new CheckBox 
            { 
                Text = "Verify", 
                X = 37, 
                Y = 4, 
                Value = _config.EnableReview ? CheckState.Checked : CheckState.UnChecked 
            };

            _checkGenerate = new CheckBox 
            { 
                Text = "Gen Doc:", 
                X = 48, 
                Y = 4, 
                Value = !string.IsNullOrEmpty(_config.DefaultOutputFormat) ? CheckState.Checked : CheckState.UnChecked 
            };

            var formatsList = _config.SupportedOutputFormats ?? new List<string> { "html", "docx", "odf", "pdf" };
            _comboFormat = new DropDownList
            {
                X = 60,
                Y = 4,
                Width = 10,
                Height = 1
            };
            _comboFormat.Source = new ListWrapper<string>(new ObservableCollection<string>(formatsList));
            _comboFormat.Text = string.IsNullOrEmpty(_config.DefaultOutputFormat) ? "html" : _config.DefaultOutputFormat;

            _labelFiles = new Label 
            { 
                Text = "Documents Found (Space/Double-Click to select):", 
                X = 1, 
                Y = 6 
            };

            _tableHeader = new Label 
            { 
                Text = "Sel | File Name            | Type | Mode                 | Pages    ", 
                X = 1, 
                Y = 7, 
                SchemeName = "Base" 
            };

            _listFiles = new ListView
            {
                X = 1,
                Y = 8,
                Width = Dim.Fill(2),
                Height = Dim.Fill(2)
            };
            _listFiles.SetSource(new ObservableCollection<string>());

            _labelShortcuts = new Label 
            { 
                Text = "Keys: [Space] Toggle Sel | [T] Toggle Mode | [P] Set Pages", 
                X = 1,
                Y = Pos.Bottom(_listFiles),
                Width = Dim.Fill(2),
                SchemeName = "Menu" 
            };

            _rightFrame.Add(
                _labelScanDir, _textScanDir, _btnScan,
                _labelTasks, _checkTranscribe, _checkTranslate, _checkVerify, _checkGenerate, _comboFormat,
                _labelFiles, _tableHeader, _listFiles, _labelShortcuts
            );

            // Bottom Actions inside Translator tab panel
            _btnStart = new Button 
            { 
                Text = "Start Translation [F9]", 
                X = Pos.Center() - 20,
                Y = Pos.AnchorEnd(1) 
            };

            _btnCancel = new Button 
            { 
                Text = "Quit [F12]", 
                X = Pos.Center() + 15,
                Y = Pos.AnchorEnd(1) 
            };

            _translatorView.Add(_leftFrame, _rightFrame, _btnStart, _btnCancel);

            // --- 2. Jobs History Tab Panel ---
            _jobsHistoryView = new View()
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Visible = false
            };

            var jobsListFrame = new FrameView 
            { 
                Title = "Past Jobs", 
                X = 0, 
                Y = 0, 
                Width = Dim.Percent(38), 
                Height = Dim.Fill(),
                BorderStyle = LineStyle.Rounded
            };

            _listJobs = new ListView
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(2),
                Height = Dim.Fill(2)
            };
            jobsListFrame.Add(_listJobs);

            var jobDetailsFrame = new FrameView 
            { 
                Title = "Job Details", 
                X = Pos.Right(jobsListFrame), 
                Y = 0, 
                Width = Dim.Fill(), 
                Height = Dim.Fill(),
                BorderStyle = LineStyle.Rounded
            };

            _textJobDetails = new SafeTextView
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill(2),
                Height = Dim.Fill(4),
                ReadOnly = true,
                WordWrap = true
            };

            _btnRetryJob = new Button
            {
                Text = "Resume/Retry Job [F9]",
                X = 1,
                Y = Pos.Bottom(_textJobDetails) + 1
            };

            _btnRefreshJobs = new Button
            {
                Text = "Refresh [F6]",
                X = Pos.Right(_btnRetryJob) + 2,
                Y = Pos.Bottom(_textJobDetails) + 1
            };

            _btnViewJob = new Button
            {
                Text = "View & Export [V]",
                X = Pos.Right(_btnRefreshJobs) + 2,
                Y = Pos.Bottom(_textJobDetails) + 1
            };

            jobDetailsFrame.Add(_textJobDetails, _btnRetryJob, _btnRefreshJobs, _btnViewJob);
            _jobsHistoryView.Add(jobsListFrame, jobDetailsFrame);

            // Add panels to tab container and elements to window
            _tabContainer.Add(_translatorView, _jobsHistoryView);
            _win.Add(_tabList, _tabContainer);
        }
    }
}
