using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Terminal.Gui;
using Terminal.Gui.Views;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;

namespace PolyglotCLI
{
    public class JobViewerDialog : Dialog
    {
        private TreeView<object>? _treeView;
        private string _jobDir;
        private AppConfig _config;
        private Terminal.Gui.App.IApplication _app;

        public JobViewerDialog(string jobDir, AppConfig config, Terminal.Gui.App.IApplication app)
        {
            _jobDir = jobDir;
            _config = config;
            _app = app;

            Title = $"Job Viewer: {Path.GetFileName(jobDir)}";
            Width = Dim.Percent(80);
            Height = Dim.Percent(80);
            BorderStyle = LineStyle.Rounded;

            BuildUI();
        }

        private void BuildUI()
        {
            _treeView = new TreeView<object>
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 2
            };
            _treeView.TreeBuilder = new JobTreeBuilder();
            _treeView.AspectGetter = (o) => 
            {
                if (o is string s) return s;
                if (o is DocumentPageData p) 
                {
                    string status = p.IsOcrSuccessful && p.IsTranslationSuccessful ? "[OK]" : "[FAIL]";
                    return $"Page {p.PageNumber} {status}";
                }
                return o.ToString() ?? "";
            };

            var files = new List<string>();
            string dataDir = Path.Combine(_jobDir, "data");
            if (Directory.Exists(dataDir))
            {
                files.AddRange(Directory.GetFiles(dataDir, "*_data.json"));
            }
            foreach (var file in Directory.GetFiles(_jobDir, "*_data.json"))
            {
                if (!files.Contains(file)) files.Add(file);
            }
            foreach (var file in files)
            {
                _treeView.AddObject(file);
            }

            var btnExport = new Button { Text = "Export to Markdown", X = Pos.Center() - 15, Y = Pos.AnchorEnd(1) };
            btnExport.Accepted += (s, e) => ExportJob();

            var btnClose = new Button { Text = "Close", X = Pos.Center() + 5, Y = Pos.AnchorEnd(1) };
            btnClose.Accepted += (s, e) => _app.RequestStop(this);

            Add(_treeView, btnExport, btnClose);
        }

        private void ExportJob()
        {
            var files = new List<string>();
            string dataDir = Path.Combine(_jobDir, "data");
            if (Directory.Exists(dataDir))
            {
                files.AddRange(Directory.GetFiles(dataDir, "*_data.json"));
            }
            foreach (var file in Directory.GetFiles(_jobDir, "*_data.json"))
            {
                if (!files.Contains(file)) files.Add(file);
            }
            int count = 0;
            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var pages = JsonSerializer.Deserialize<List<DocumentPageData>>(json);
                    if (pages != null && pages.Count > 0)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file).Replace("_data", "");
                        
                        string originalOut = Path.Combine(_config.OutputDirectory, $"{fileName}_original.md");
                        MarkdownWriter.ExportToMarkdown(originalOut, fileName, "Original", pages, true);

                        string translatedOut = Path.Combine(_config.OutputDirectory, $"{fileName}_{_config.TargetLanguage}.md");
                        MarkdownWriter.ExportToMarkdown(translatedOut, fileName, _config.TargetLanguage, pages, false);
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery(_app, "Export Error", $"Failed to export {file}:\n{ex.Message}", new[] { "OK" });
                }
            }

            MessageBox.Query(_app, "Export", $"Exported {count} documents to {_config.OutputDirectory}", new[] { "OK" });
        }
    }

    public class JobTreeBuilder : ITreeBuilder<object>
    {
        public bool SupportsCanExpand => true;

        public bool CanExpand(object toExpand)
        {
            return toExpand is string && File.Exists((string)toExpand);
        }

        public IEnumerable<object> GetChildren(object forObject)
        {
            if (forObject is string filePath && File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    var pages = JsonSerializer.Deserialize<List<DocumentPageData>>(json);
                    if (pages != null)
                    {
                        return pages;
                    }
                }
                catch { }
            }
            else if (forObject is DocumentPageData page)
            {
                var details = new List<string>();
                details.Add($"OCR Success: {page.IsOcrSuccessful}");
                if (!page.IsOcrSuccessful) details.Add($"OCR Error: {page.OcrErrorMessage}");
                
                details.Add($"Translation Success: {page.IsTranslationSuccessful}");
                if (!page.IsTranslationSuccessful) details.Add($"Translation Error: {page.TranslationErrorMessage}");
                
                details.Add($"Used Temperature: {page.UsedTemperature}");
                details.Add($"Original Text Length: {page.OriginalText?.Length ?? 0}");
                details.Add($"Translated Text Length: {page.TranslatedText?.Length ?? 0}");
                return details;
            }

            return Array.Empty<object>();
        }
    }
}
