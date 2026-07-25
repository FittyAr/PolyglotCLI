using System;
using System.IO;
using System.Linq;
using Xunit;
using PolyglotCLI;

namespace PolyglotCLI.test
{
    /// <summary>
    /// Tests para la validación de JobId y la detección de
    /// inconsistencias manifest↔disco.
    ///
    /// Estos tests cubren la defensa contra path traversal: si un
    /// manifest fue editado a mano con un JobId malicioso
    /// (ej: '..\..\..\Windows\System32'), el sistema debe
    /// rechazarlo y no tocar el filesystem.
    /// </summary>
    public class JobManifestServiceTests
    {
        // ── IsValidJobId: tests de validación de input ──────────────

        [Theory]
        [InlineData("20260710_223809")]      // formato normal
        [InlineData("20260710_223809_old")]  // sufijo _old
        [InlineData("custom-job-1")]         // con guiones
        [InlineData("a")]                    // mínimo
        [InlineData("1234567890")]           // solo dígitos
        public void IsValidJobId_AcceptsLegitimateJobIds(string jobId)
        {
            Assert.True(JobManifestService.IsValidJobId(jobId));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("..")]
        [InlineData("../foo")]
        [InlineData("..\\foo")]
        [InlineData("foo/../bar")]
        [InlineData("foo\\bar")]
        [InlineData("/etc/passwd")]          // absolute path unix
        [InlineData("C:\\Windows")]          // absolute path windows
        [InlineData("foo/bar")]              // sub-path
        [InlineData("a\u0000b")]             // NUL char
        [InlineData("a\u001fb")]             // control char
        public void IsValidJobId_RejectsPathTraversalAndMalformedInput(string? jobId)
        {
            Assert.False(JobManifestService.IsValidJobId(jobId));
        }

        [Fact]
        public void IsValidJobId_RejectsJobIdLongerThan200Chars()
        {
            string longId = new string('a', 201);
            Assert.False(JobManifestService.IsValidJobId(longId));
        }

        [Fact]
        public void IsValidJobId_AcceptsJobIdExactly200Chars()
        {
            string id = new string('a', 200);
            Assert.True(JobManifestService.IsValidJobId(id));
        }

        // ── TryResolveJobDirectory: tests sin tocar filesystem real ─
        //
        // Estos tests validan la rama de "rechazo por input inválido"
        // sin crear directorios reales (porque GetJobsDirectory()
        // devuelve %AppData% y no podemos monkey-patch el método
        // estático). Para los tests de "dir existe/no existe" en
        // el path real, ver los integration tests manuales.

        [Theory]
        [InlineData("..\\..\\Windows\\System32")]
        [InlineData("../etc/passwd")]
        [InlineData("foo/bar")]
        [InlineData("")]
        [InlineData(null)]
        public void TryResolveJobDirectory_RejectsPathTraversalAndInvalidInput(string? jobId)
        {
            var resolution = JobManifestService.TryResolveJobDirectory(jobId ?? "");

            Assert.False(resolution.IsConsistent);
            Assert.Null(resolution.ActualPath);
        }

        [Fact]
        public void TryResolveJobDirectory_RejectsPathTraversalEvenIfDirExists()
        {
            // Aunque la carpeta "..\foo" exista en algún lado
            // (lo cual sería un signo de compromise), el resolver
            // NO la va a tomar como válida. Esto evita que un
            // manifest malicioso o editado a mano pueda escapar
            // del directorio de jobs.
            var resolution = JobManifestService.TryResolveJobDirectory("..\\..\\Windows\\System32");

            Assert.False(resolution.IsConsistent);
            Assert.Null(resolution.ActualPath);
        }

        [Fact]
        public void TryResolveJobDirectory_ReturnsResolutionWithExpectedPath()
        {
            // Para un JobId válido que NO existe, el resultado tiene
            // ActualPath=null pero el ExpectedPath está bien armado
            // (no se escapa del jobs root). Esto es importante para
            // que la UI pueda mostrarle al usuario dónde esperaba
            // encontrar la carpeta.
            var resolution = JobManifestService.TryResolveJobDirectory("nonexistent_20260710_223809");

            Assert.False(resolution.IsConsistent);
            Assert.Null(resolution.ActualPath);
            // El ExpectedPath NO debe contener ".."
            Assert.DoesNotContain("..", resolution.ExpectedPath);
            // Debe terminar con el JobId
            Assert.EndsWith("nonexistent_20260710_223809", resolution.ExpectedPath);
        }
    }
}
