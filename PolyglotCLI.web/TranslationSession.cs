using System;
using System.Collections.Generic;
using System.Threading;
using PolyglotCLI.web.Components.Pages;

namespace PolyglotCLI.web
{
    public static class TranslationSession
    {
        public static bool IsProcessing { get; set; } = false;
        public static CancellationTokenSource? Cts { get; set; }
        public static List<LogEntry> Logs { get; } = new List<LogEntry>();
    }
}
