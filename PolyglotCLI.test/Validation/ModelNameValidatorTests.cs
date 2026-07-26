using Xunit;
using PolyglotCLI.Validation;

namespace PolyglotCLI.test.Validation
{
    public class ModelNameValidatorTests
    {
        // ── SanitizeModelName ──────────────────────────────────

        [Theory]
        [InlineData("qwen/qwen2.5-7b")]
        [InlineData("llama3.1:8b-instruct-q4_K_M")]
        [InlineData("claude-3-5-sonnet-20241022")]
        [InlineData("gpt-4o")]
        [InlineData("gemini-1.5-pro")]
        [InlineData("kimi-k2-0711-preview")]
        [InlineData("MiniMax-Text-01")]
        [InlineData("custom-model-name_v1.0+experimental")]
        public void SanitizeModelName_AcceptsLegitimateModelNames(string name)
        {
            var result = ModelNameValidator.SanitizeModelName(name);
            Assert.True(result.IsValid);
            Assert.Equal(name, result.Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("model with space")]
        [InlineData("model\twith\ttab")]
        [InlineData("model\nwith\nnewline")]
        [InlineData("model$injection")]
        [InlineData("model`backtick`")]
        [InlineData("model;semicolon")]
        [InlineData("model|pipe")]
        [InlineData("model&amp;")]
        [InlineData("model<script>")]
        [InlineData("model*glob*")]
        [InlineData("model?question?")]
        [InlineData("model~tilde~")]
        public void SanitizeModelName_RejectsShellMetachars(string? name)
        {
            var result = ModelNameValidator.SanitizeModelName(name);
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData(".model")]
        [InlineData("-model")]
        [InlineData("_model")]
        [InlineData("/model")]
        [InlineData(":model")]
        [InlineData("+model")]
        public void SanitizeModelName_RejectsLeadingSeparators(string name)
        {
            var result = ModelNameValidator.SanitizeModelName(name);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void SanitizeModelName_RejectsTooLong()
        {
            string longName = new string('a', 201);
            var result = ModelNameValidator.SanitizeModelName(longName);
            Assert.False(result.IsValid);
        }

        // ── SanitizeProviderName ────────────────────────────────

        [Theory]
        [InlineData("OpenAI")]
        [InlineData("Ollama")]
        [InlineData("LM Studio")]
        [InlineData("LM_Studio")]
        [InlineData("Google-Gemini")]
        [InlineData("Custom")]
        public void SanitizeProviderName_AcceptsLegitimate(string name)
        {
            var result = ModelNameValidator.SanitizeProviderName(name);
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("provider/slash")]
        [InlineData("provider:colon")]
        [InlineData("provider.dot")]
        [InlineData("provider!bang")]
        public void SanitizeProviderName_RejectsForbiddenChars(string name)
        {
            var result = ModelNameValidator.SanitizeProviderName(name);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void SanitizeProviderName_AcceptsMaxLength()
        {
            string name = new string('a', 50);
            var result = ModelNameValidator.SanitizeProviderName(name);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void SanitizeProviderName_RejectsTooLong()
        {
            string name = new string('a', 51);
            var result = ModelNameValidator.SanitizeProviderName(name);
            Assert.False(result.IsValid);
        }
    }
}
