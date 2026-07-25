using System;
using System.IO;
using PolyglotCLI.web.Services.JobDetails;

namespace PolyglotCLI.test;

/// <summary>
/// Cubre la defensa contra path traversal del JobArtifactsService.
/// Antes del fix, los métodos ReadTextFile/ReadFileAsBase64 abrían
/// cualquier path que recibieran — si un manifest manipulado proveía
/// rutas absolutas a <c>config.json</c> u otros archivos del usuario,
/// el contenido viajaba al cliente Blazor en base64.
/// </summary>
public class JobArtifactsServiceTests : IDisposable
{
    private readonly string _jobDir;
    private readonly string _outsideDir;
    private readonly IJobArtifactsService _svc = new JobArtifactsService();

    public JobArtifactsServiceTests()
    {
        _jobDir = Path.Combine(Path.GetTempPath(), $"job_arts_{Guid.NewGuid():N}");
        _outsideDir = Path.Combine(Path.GetTempPath(), $"job_arts_outside_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_jobDir, "temp"));
        Directory.CreateDirectory(_outsideDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_jobDir)) Directory.Delete(_jobDir, recursive: true); } catch { }
        try { if (Directory.Exists(_outsideDir)) Directory.Delete(_outsideDir, recursive: true); } catch { }
    }

    [Fact]
    public void ReadTextFile_RejectsAbsolutePathOutsideJobDir()
    {
        // Un manifest manipulado podría pasar una ruta absoluta a
        // config.json (que contiene la API key en texto plano).
        string outsideFile = Path.Combine(_outsideDir, "secret.txt");
        File.WriteAllText(outsideFile, "API-KEY-LEAKED");

        var result = _svc.ReadTextFile(_jobDir, outsideFile);

        Assert.NotNull(result);
        Assert.StartsWith("Error", result);
        Assert.Contains("fuera del directorio del trabajo", result);
    }

    [Fact]
    public void ReadTextFile_RejectsRelativeTraversal()
    {
        // "../../../Windows/System32/..." debe caer fuera.
        var result = _svc.ReadTextFile(_jobDir, "..\\..\\Windows\\System32\\drivers\\etc\\hosts");

        Assert.NotNull(result);
        Assert.Contains("fuera del directorio del trabajo", result);
    }

    [Fact]
    public void ReadFileAsBase64_RejectsAbsolutePathOutsideJobDir()
    {
        string outsideFile = Path.Combine(_outsideDir, "secret.bin");
        File.WriteAllBytes(outsideFile, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

        var result = _svc.ReadFileAsBase64(_jobDir, outsideFile);

        Assert.NotNull(result);
        Assert.StartsWith("Error", result);
        Assert.Contains("fuera del directorio del trabajo", result);
    }

    [Fact]
    public void ReadTextFile_AcceptsFileInsideJobDir()
    {
        string log = Path.Combine(_jobDir, "logs", "app.log");
        Directory.CreateDirectory(Path.Combine(_jobDir, "logs"));
        File.WriteAllText(log, "log line 1\nlog line 2\n");

        var result = _svc.ReadTextFile(_jobDir, log);

        Assert.NotNull(result);
        Assert.Equal("log line 1\nlog line 2\n", result);
    }

    [Fact]
    public void ReadFileAsBase64_AcceptsFileInsideJobDir()
    {
        string png = Path.Combine(_jobDir, "temp", "page_1.png");
        byte[] bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        File.WriteAllBytes(png, bytes);

        var result = _svc.ReadFileAsBase64(_jobDir, png);

        Assert.NotNull(result);
        Assert.Equal(Convert.ToBase64String(bytes), result);
    }

    [Fact]
    public void ReadTextFile_RejectsTraversalEvenWithValidPrefix()
    {
        // Caso límite: el path absoluto empieza con el jobDir pero
        // tiene sufijos que escapan (jobDir\..\outside). GetFullPath
        // lo normaliza y debe detectarlo.
        string tricky = _jobDir + @"\..\..\outside.txt";
        // Crear el archivo donde efectivamente apuntaría el path
        // resuelto para asegurarnos de que la única defensa es la
        // validación del servicio, no la ausencia del archivo.
        string realOutside = Path.GetFullPath(tricky);
        Directory.CreateDirectory(Path.GetDirectoryName(realOutside)!);
        File.WriteAllText(realOutside, "should-not-be-readable");
        try
        {
            var result = _svc.ReadTextFile(_jobDir, tricky);
            Assert.NotNull(result);
            Assert.Contains("fuera del directorio del trabajo", result);
        }
        finally
        {
            try { File.Delete(realOutside); } catch { }
        }
    }

    [Fact]
    public void ReadFileAsBase64_RejectsTraversalEvenWithValidPrefix()
    {
        string tricky = _jobDir + @"\..\..\outside.bin";
        string realOutside = Path.GetFullPath(tricky);
        Directory.CreateDirectory(Path.GetDirectoryName(realOutside)!);
        File.WriteAllBytes(realOutside, new byte[] { 1, 2, 3 });
        try
        {
            var result = _svc.ReadFileAsBase64(_jobDir, tricky);
            Assert.NotNull(result);
            Assert.Contains("fuera del directorio del trabajo", result);
        }
        finally
        {
            try { File.Delete(realOutside); } catch { }
        }
    }

    [Fact]
    public void ReadTextFile_RejectsEmptyInputs()
    {
        // Inputs vacíos caen en la rama de "ruta inválida" y devuelven
        // el prefijo de error, no throw. Esto es importante: la UI
        // muestra el resultado en un toast y un null sería peor que un
        // mensaje legible.
        var r1 = _svc.ReadTextFile("", "x");
        Assert.NotNull(r1);
        Assert.Contains("Error", r1);

        var r2 = _svc.ReadTextFile(_jobDir, "");
        Assert.NotNull(r2);
        Assert.Contains("Error", r2);
    }
}
