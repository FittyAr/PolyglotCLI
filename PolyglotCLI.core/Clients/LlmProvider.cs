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
                LlmProvider.MiniMax => "https://api.minimax.io/v1",
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

        public static System.Collections.Generic.List<string> GetDefaultSuggestedModels(LlmProvider provider)
        {
            return provider switch
            {
                LlmProvider.Ollama => new System.Collections.Generic.List<string> { "llama3.1:8b", "gemma2:9b", "mistral:7b", "phi3:medium", "qwen2:7b" },
                LlmProvider.LmStudio => new System.Collections.Generic.List<string> { "meta-llama-3-8b-instruct", "microsoft-phi-3-medium-4k-instruct", "mistral-7b-instruct" },
                LlmProvider.LlamaCpp => new System.Collections.Generic.List<string> { "meta-llama-3-8b-instruct", "microsoft-phi-3-medium-4k-instruct" },
                LlmProvider.OpenAi => new System.Collections.Generic.List<string> { "gpt-4o-mini", "gpt-4o", "gpt-4-turbo", "gpt-3.5-turbo" },
                LlmProvider.Gemini => new System.Collections.Generic.List<string> { "gemini-1.5-flash", "gemini-1.5-pro", "gemini-1.0-pro" },
                LlmProvider.Qwen => new System.Collections.Generic.List<string> { "qwen-turbo", "qwen-plus", "qwen-max" },
                LlmProvider.Kimi => new System.Collections.Generic.List<string> { "moonshot-v1-8k", "moonshot-v1-32k", "moonshot-v1-128k" },
                LlmProvider.MiniMax => new System.Collections.Generic.List<string> { "MiniMax-M3", "MiniMax-M2.7", "MiniMax-M2.5", "MiniMax-M2.1", "MiniMax-M2" },
                _ => new System.Collections.Generic.List<string>()
            };
        }

        public static System.Collections.Generic.List<string> GetDefaultSuggestedVisionModels(LlmProvider provider)
        {
            return provider switch
            {
                LlmProvider.Ollama => new System.Collections.Generic.List<string> { "llava:7b", "llama3-vision", "bakllava" },
                LlmProvider.LmStudio => new System.Collections.Generic.List<string> { "meta-llama-3-8b-instruct" },
                LlmProvider.LlamaCpp => new System.Collections.Generic.List<string> { "meta-llama-3-8b-instruct" },
                LlmProvider.OpenAi => new System.Collections.Generic.List<string> { "gpt-4o", "gpt-4o-mini", "gpt-4-vision-preview" },
                LlmProvider.Gemini => new System.Collections.Generic.List<string> { "gemini-1.5-flash", "gemini-1.5-pro" },
                LlmProvider.Qwen => new System.Collections.Generic.List<string> { "qwen-vl-max", "qwen-vl-plus" },
                LlmProvider.Kimi => new System.Collections.Generic.List<string> { "moonshot-v1-8k" },
                LlmProvider.MiniMax => new System.Collections.Generic.List<string> { "MiniMax-M3", "MiniMax-M2.7" },
                _ => new System.Collections.Generic.List<string>()
            };
        }
    }
}
