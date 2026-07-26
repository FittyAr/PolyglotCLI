using Xunit;
using PolyglotCLI.Validation;

namespace PolyglotCLI.test.Validation
{
    public class PromptValidatorTests
    {
        // ── SanitizePrompt ─────────────────────────────────────

        [Theory]
        [InlineData("Translate this text to Spanish.")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("Multi\nline\nprompt\nwith\nnewlines")]
        [InlineData("Tab\there")]
        [InlineData("Carriage\rreturn")]
        public void SanitizePrompt_AcceptsLegitimatePrompts(string? prompt)
        {
            var result = PromptValidator.SanitizePrompt(prompt);
            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("prompt\u0000with NUL")]
        [InlineData("prompt\u0001with control char")]
        [InlineData("prompt\u0007with bell")]
        [InlineData("prompt\u001bwith escape")]
        [InlineData("prompt\u007fwith DEL")]
        public void SanitizePrompt_RejectsControlChars(string prompt)
        {
            var result = PromptValidator.SanitizePrompt(prompt);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void SanitizePrompt_RejectsTooLong()
        {
            string longPrompt = new string('a', 50_001);
            var result = PromptValidator.SanitizePrompt(longPrompt);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void SanitizePrompt_AcceptsMaxLength()
        {
            string maxPrompt = new string('a', 50_000);
            var result = PromptValidator.SanitizePrompt(maxPrompt);
            Assert.True(result.IsValid);
        }

        [Fact]
        public void SanitizePrompt_AcceptsCustomMaxLength()
        {
            string prompt = new string('a', 100);
            var result = PromptValidator.SanitizePrompt(prompt, maxLength: 50);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void SanitizePrompt_TranslationContent_NotFlaggedAsInjection()
        {
            // Documentos de traducción pueden contener estas
            // frases legítimamente. No deben ser flagged.
            string prompt = "Translate the following IT manual. " +
                           "Note: this document includes sections that say things like " +
                           "'ignore previous configurations' and 'system requirements' " +
                           "as part of the technical content. Please translate all of it.";

            var result = PromptValidator.SanitizePrompt(prompt);
            Assert.True(result.IsValid);
        }

        // ── DetectInjectionAttempts (placeholder) ──────────────

        [Fact]
        public void DetectInjectionAttempts_ReturnsEmptyArray()
        {
            // Por diseño (ver docs/input-validation-plan.md).
            // Esta función está intencionalmente sin implementar.
            var result = PromptValidator.DetectInjectionAttempts("anything");
            Assert.Empty(result);
        }
    }
}
