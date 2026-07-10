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
        private readonly AppConfig _config;
        private readonly List<SelectableFile> _filesSource = new();
        private CommandLineOptions? _resultOptions;
        private IApplication? _app;
        /// <summary>
        /// True while any modal dialog is being shown via <c>AppRequired.Run(dialog)</c>.
        /// Used by the global keyboard handler to avoid consuming character keys meant for modal inputs.
        /// </summary>
        private bool _isModalOpen;

        // UI Widget fields — initialized in BuildLayout(), declared nullable to satisfy CS8618
        private Window? _win;
        private FrameView? _leftFrame;
        private FrameView? _rightFrame;
        
        private SafeTextView? _textAddPrompt;
        private Button? _btnImprovePrompt;
        private Button? _btnAnalyzeFilePrompt;
        private Button? _btnSavePresets;
        private Button? _btnConfig;

        private Label? _labelScanDir;
        private TextField? _textScanDir;
        private Button? _btnScan;

        private Label? _labelTasks;
        private CheckBox? _checkTranscribe;
        private CheckBox? _checkTranslate;
        private CheckBox? _checkVerify;
        private CheckBox? _checkGenerate;
        private DropDownList? _comboFormat;

        private Label? _labelFiles;
        private Label? _tableHeader;
        private ListView? _listFiles;
        private Label? _labelShortcuts;

        private Button? _btnStart;
        private Button? _btnCancel;

        public InteractiveMenu(AppConfig config)
        {
            _config = config;
        }

        public static async Task<CommandLineOptions?> RunAsync(AppConfig config)
        {
            var menu = new InteractiveMenu(config);
            return await menu.RunInternalAsync();
        }

        /// <summary>
        /// Returns the current <see cref="IApplication"/> instance.
        /// Throws <see cref="InvalidOperationException"/> when called outside the application lifetime.
        /// Callers in Actions/Events/Dialogs are always invoked from within the TUI loop, so this is safe.
        /// </summary>
        private IApplication AppRequired
            => _app ?? throw new InvalidOperationException("Application is not initialized.");

        private async Task<CommandLineOptions?> RunInternalAsync()
        {
            // 1. Initialise Terminal.Gui cleanly using v2 instance block
            using (var app = Application.Create().Init())
            {
                _app = app;

                // 2. Build UI layout
                BuildLayout();

                // 3. Wire event handlers
                WireEvents();

                // 4. Perform scan of starting directory on startup if it exists
                if (Directory.Exists(_config.LastScanDirectory ?? "."))
                {
                    _win!.Initialized += (s, e) => {
                        PerformScan();
                    };
                }

                // 5. Run the main application loop
                _app.Run(_win!);

                // 6. Dispose window
                _win!.Dispose();
                _app = null;
            }

            return _resultOptions;
        }
    }
}
