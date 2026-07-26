using System;
using Xunit;
using PolyglotCLI.Validation;

namespace PolyglotCLI.test.Validation
{
    public class NetworkUrlValidatorTests
    {
        // ── SanitizeApiUrl ─────────────────────────────────────

        [Theory]
        [InlineData("http://localhost:1234/v1")]
        [InlineData("https://api.openai.com/v1")]
        [InlineData("http://192.168.0.11:1234/v1")]
        [InlineData("https://generativelanguage.googleapis.com/v1beta")]
        public void SanitizeApiUrl_AcceptsValidHttpUrls(string url)
        {
            var result = NetworkUrlValidator.SanitizeApiUrl(url);
            Assert.True(result.IsValid);
            Assert.NotNull(result.Value);
            Assert.Equal(url, result.Value!.ToString());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-a-url")]
        [InlineData("ftp://server.com")]
        [InlineData("file:///etc/passwd")]
        [InlineData("gopher://internal")]
        public void SanitizeApiUrl_RejectsInvalid(string? url)
        {
            var result = NetworkUrlValidator.SanitizeApiUrl(url);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void SanitizeApiUrl_RejectsTooLong()
        {
            string longUrl = "https://example.com/" + new string('a', 3000);
            var result = NetworkUrlValidator.SanitizeApiUrl(longUrl);
            Assert.False(result.IsValid);
        }

        // ── IsPrivateOrLocalhost ───────────────────────────────

        [Theory]
        [InlineData("http://localhost/v1")]
        [InlineData("http://127.0.0.1:8080/v1")]
        [InlineData("http://[::1]/v1")]
        [InlineData("http://10.0.0.5/v1")]
        [InlineData("http://192.168.1.1/v1")]
        [InlineData("http://172.16.0.1/v1")]
        [InlineData("http://172.31.255.255/v1")]
        [InlineData("http://169.254.169.254/latest/meta-data")]  // AWS metadata
        [InlineData("http://0.0.0.0/v1")]
        [InlineData("http://[fc00::1]/v1")]                       // IPv6 ULA
        [InlineData("http://[fe80::1]/v1")]                       // IPv6 link-local
        public void IsPrivateOrLocalhost_True_ForPrivateOrLocal(string url)
        {
            var uri = new Uri(url);
            Assert.True(NetworkUrlValidator.IsPrivateOrLocalhost(uri));
        }

        [Theory]
        [InlineData("https://api.openai.com/v1")]
        [InlineData("https://generativelanguage.googleapis.com/v1beta")]
        [InlineData("https://api.anthropic.com/v1")]
        [InlineData("https://api.deepseek.com/v1")]
        public void IsPrivateOrLocalhost_False_ForPublicHosts(string url)
        {
            var uri = new Uri(url);
            Assert.False(NetworkUrlValidator.IsPrivateOrLocalhost(uri));
        }

        // ── HasValidScheme ─────────────────────────────────────

        [Theory]
        [InlineData("http://x.com")]
        [InlineData("https://x.com")]
        public void HasValidScheme_True_ForHttpAndHttps(string url)
        {
            var uri = new Uri(url);
            Assert.True(NetworkUrlValidator.HasValidScheme(uri));
        }

        [Theory]
        [InlineData("ftp://x.com")]
        [InlineData("file:///x")]
        public void HasValidScheme_False_ForOtherSchemes(string url)
        {
            var uri = new Uri(url);
            Assert.False(NetworkUrlValidator.HasValidScheme(uri));
        }
    }
}
