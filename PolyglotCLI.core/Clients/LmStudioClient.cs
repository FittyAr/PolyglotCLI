using System;

namespace PolyglotCLI
{
    public class LmStudioClient : OpenAiCompatibleClient
    {
        public LmStudioClient(string apiUrl, int timeoutSeconds = 300)
            : base(apiUrl, null, timeoutSeconds)
        {
        }

        public LmStudioClient(string apiUrl, string? apiKey, int timeoutSeconds = 300)
            : base(apiUrl, apiKey, timeoutSeconds)
        {
        }
    }
}
