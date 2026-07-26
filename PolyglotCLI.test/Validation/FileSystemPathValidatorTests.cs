using Xunit;
using PolyglotCLI.Validation;

namespace PolyglotCLI.test.Validation
{
    public class FileSystemPathValidatorTests
    {
        // ── ContainsPathTraversal ──────────────────────────────

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("normal/path/file.txt")]
        [InlineData("C:\\Users\\foo\\docs")]
        [InlineData("file.txt")]
        public void ContainsPathTraversal_ReturnsFalse_ForSafeInputs(string? path)
        {
            Assert.False(FileSystemPathValidator.ContainsPathTraversal(path));
        }

        [Theory]
        [InlineData("..")]
        [InlineData("../foo")]
        [InlineData("foo/..")]
        [InlineData("foo/../bar")]
        [InlineData("..\\foo")]
        [InlineData("foo\\..\\bar")]
        [InlineData("a/b/../c")]
        public void ContainsPathTraversal_ReturnsTrue_ForTraversalSequences(string path)
        {
            Assert.True(FileSystemPathValidator.ContainsPathTraversal(path));
        }

        // ── SanitizeFileName ───────────────────────────────────

        [Theory]
        [InlineData("document.pdf")]
        [InlineData("my file.txt")]
        [InlineData("data.csv")]
        [InlineData("a")]
        [InlineData("file-name_2026.tar.gz")]
        public void SanitizeFileName_AcceptsLegitimateNames(string name)
        {
            var result = FileSystemPathValidator.SanitizeFileName(name);
            Assert.True(result.IsValid);
            Assert.Equal(name, result.Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("..")]
        [InlineData("foo/bar.txt")]
        [InlineData("foo\\bar.txt")]
        [InlineData("a\u0000b.txt")]
        [InlineData("file<name>")]
        [InlineData("file|name")]
        [InlineData("file?name")]
        [InlineData("file*name")]
        [InlineData("file\"name")]
        public void SanitizeFileName_RejectsInvalid(string? name)
        {
            var result = FileSystemPathValidator.SanitizeFileName(name);
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void SanitizeFileName_RejectsTooLong()
        {
            string longName = new string('a', 256);
            var result = FileSystemPathValidator.SanitizeFileName(longName);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void SanitizeFileName_AcceptsMaxLength()
        {
            string maxName = new string('a', 255);
            var result = FileSystemPathValidator.SanitizeFileName(maxName);
            Assert.True(result.IsValid);
        }

        // ── SanitizeDirectoryPath ──────────────────────────────

        [Theory]
        [InlineData("C:\\Users\\foo\\docs")]
        [InlineData("/home/user/docs")]
        [InlineData("relative/path")]
        [InlineData("a")]
        public void SanitizeDirectoryPath_AcceptsLegitimatePaths(string path)
        {
            var result = FileSystemPathValidator.SanitizeDirectoryPath(path);
            Assert.True(result.IsValid);
            Assert.Equal(path, result.Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("..\\foo")]
        [InlineData("../foo")]
        [InlineData("foo/../bar")]
        [InlineData("path\u0000nul")]
        public void SanitizeDirectoryPath_RejectsInvalid(string? path)
        {
            var result = FileSystemPathValidator.SanitizeDirectoryPath(path);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void SanitizeDirectoryPath_MustExistTrue_FailsForMissingPath()
        {
            // Probamos con un path que NO existe (C:\__polyglot_test_no_existe__)
            var result = FileSystemPathValidator.SanitizeDirectoryPath(
                "C:\\__polyglot_test_no_existe__", mustExist: true);
            Assert.False(result.IsValid);
        }

        // ── SanitizeFileExtension ──────────────────────────────

        [Theory]
        [InlineData(".pdf")]
        [InlineData(".docx")]
        [InlineData(".tar.gz")]
        [InlineData("pdf")]
        [InlineData("docx")]
        public void SanitizeFileExtension_AcceptsLegitimate(string ext)
        {
            var result = FileSystemPathValidator.SanitizeFileExtension(ext);
            Assert.True(result.IsValid);
            // Debe normalizar para incluir el punto inicial
            Assert.StartsWith(".", result.Value);
        }

        [Theory]
        [InlineData("")]
        [InlineData(".pdf/evil")]
        [InlineData(".pd\u0000f")]
        [InlineData(".p df")]
        [InlineData(".p|f")]
        [InlineData(".p<f>")]
        public void SanitizeFileExtension_RejectsInvalid(string? ext)
        {
            var result = FileSystemPathValidator.SanitizeFileExtension(ext);
            Assert.False(result.IsValid);
        }

        // ── IsAbsolutePath ─────────────────────────────────────

        [Theory]
        [InlineData("C:\\Users\\foo")]
        [InlineData("D:/test")]
        [InlineData("/home/user")]
        [InlineData("\\\\server\\share")]
        public void IsAbsolutePath_True_ForAbsolutePaths(string path)
        {
            Assert.True(FileSystemPathValidator.IsAbsolutePath(path));
        }

        [Theory]
        [InlineData("relative")]
        [InlineData("foo/bar")]
        [InlineData("")]
        [InlineData(null)]
        public void IsAbsolutePath_False_ForRelativeOrEmpty(string? path)
        {
            Assert.False(FileSystemPathValidator.IsAbsolutePath(path));
        }
    }
}
