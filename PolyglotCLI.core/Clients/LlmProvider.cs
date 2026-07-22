using System;

namespace PolyglotCLI
{
    public enum LlmProvider
    {
        Ollama,
        LmStudio,
        LlamaCpp,
        OpenAi,
        Gemini,
        Qwen,
        Kimi,
        MiniMax,
        Custom
    }

    public static class LlmProviderHelper
    {
        public static string GetDefaultApiUrl(LlmProvider provider)
        {
            return provider switch
            {
                LlmProvider.Ollama => "http://localhost:11434/v1",
                LlmProvider.LmStudio => "http://localhost:1234/v1",
                LlmProvider.LlamaCpp => "http://localhost:8080/v1",
                LlmProvider.OpenAi => "https://api.openai.com/v1",
                LlmProvider.Gemini => "https://generativelanguage.googleapis.com/v1beta",
                LlmProvider.Qwen => "https://dashscope.aliyuncs.com/compatible-mode/v1",
                LlmProvider.Kimi => "https://api.moonshot.cn/v1",
                LlmProvider.MiniMax => "https://api.minimax.chat/v1",
                LlmProvider.Custom => "http://localhost:1234/v1",
                _ => "http://localhost:1234/v1"
            };
        }

        public static bool RequiresApiKey(LlmProvider provider)
        {
            return provider switch
            {
                LlmProvider.OpenAi => true,
                LlmProvider.Gemini => true,
                LlmProvider.Qwen => true,
                LlmProvider.Kimi => true,
                LlmProvider.MiniMax => true,
                _ => false
            };
        }

        public static bool SupportsVramUnload(LlmProvider provider)
        {
            return provider switch
            {
                LlmProvider.LmStudio => true,
                LlmProvider.Ollama => true,
                _ => false
            };
        }

        public static LlmProvider ParseProvider(string? providerStr)
        {
            if (string.IsNullOrWhiteSpace(providerStr))
                return LlmProvider.LmStudio;

            if (Enum.TryParse<LlmProvider>(providerStr, true, out var provider))
                return provider;

            string normalized = providerStr.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
            return normalized switch
            {
                "ollama" => LlmProvider.Ollama,
                "lmstudio" => LlmProvider.LmStudio,
                "llamacpp" or "llama" => LlmProvider.LlamaCpp,
                "openai" or "opencode" => LlmProvider.OpenAi,
                "gemini" or "google" => LlmProvider.Gemini,
                "qwen" or "dashscope" => LlmProvider.Qwen,
                "kimi" or "moonshot" => LlmProvider.Kimi,
                "minimax" => LlmProvider.MiniMax,
                _ => LlmProvider.Custom
            };
        }
    }
}
