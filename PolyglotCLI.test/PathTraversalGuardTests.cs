using System;
using System.IO;
using PolyglotCLI.Validation;
using Xunit;

namespace PolyglotCLI.test;

/// <summary>
/// Tests directos de <see cref="PathTraversalGuard.TryResolveInside"/>,
/// la primitiva de seguridad compartida entre
/// <c>JobArtifactsService</c> (web) y <c>JobPackageService</c> (core).
///
/// <para>Antes había 2 implementaciones casi idénticas; este helper
/// es la única fuente de verdad. Los tests cubren los vectores
/// clásicos de path traversal más un caso que rompió
/// implementaciones previas: un root que es prefijo de otro path
/// (ej: root=<c>/foo</c>, path=<c>/foobar</c>).</para>
/// </summary>
public class PathTraversalGuardTests : IDisposable
{
    private readonly string _root;

    public PathTraversalGuardTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"ptg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void TryResolveInside_RelativePath_Inside_ReturnsTrue()
    {
        Assert.True(PathTraversalGuard.TryResolveInside(_root, "file.txt", out var resolved));
        Assert.Equal(Path.Combine(_root, "file.txt"), resolved);
    }

    [Fact]
    public void TryResolveInside_NestedRelativePath_ReturnsTrue()
    {
        Assert.True(PathTraversalGuard.TryResolveInside(_root, "sub/dir/file.txt", out var resolved));
        Assert.StartsWith(_root + Path.DirectorySeparatorChar, resolved);
    }

    [Fact]
    public void TryResolveInside_PathTraversal_DotDot_Blocked()
    {
        Assert.False(PathTraversalGuard.TryResolveInside(_root, "../etc/passwd", out var resolved));
        Assert.Equal(string.Empty, resolved);
    }

    [Fact]
    public void TryResolveInside_PathTraversal_Deep_DotDot_Blocked()
    {
        Assert.False(PathTraversalGuard.TryResolveInside(_root, "../../../../../../etc/passwd", out _));
    }

    [Fact]
    public void TryResolveInside_AbsolutePath_Inside_ReturnsTrue()
    {
        string inside = Path.Combine(_root, "child.txt");
        Assert.True(PathTraversalGuard.TryResolveInside(_root, inside, out var resolved));
        Assert.Equal(inside, resolved);
    }

    [Fact]
    public void TryResolveInside_AbsolutePath_Outside_Blocked()
    {
        string outside = Path.GetTempPath(); // algún path garantizado fuera de _root
        Assert.False(PathTraversalGuard.TryResolveInside(_root, outside, out _));
    }

    [Fact]
    public void TryResolveInside_RootIsPrefixOfAnotherPath_Blocked()
    {
        // El bug clásico de "StartsWith(root) acepta /foo cuando root
        // es /foo". Acá simulamos con un root que casualmente matchea
        // el inicio de un path distinto.
        string rootParent = Path.GetDirectoryName(_root.TrimEnd(Path.DirectorySeparatorChar))!;
        Assert.False(string.IsNullOrEmpty(rootParent));
        // Construimos un path dentro de rootParent que empieza con
        // el nombre de _root como substring.
        string tricky = Path.Combine(rootParent, Path.GetFileName(_root) + "_sneaky", "file.txt");
        Assert.False(PathTraversalGuard.TryResolveInside(_root, tricky, out _),
            "Path cuyo nombre empieza con root debe ser rechazado.");
    }

    [Fact]
    public void TryResolveInside_RootItself_ReturnsTrue()
    {
        // El root mismo cuenta como "dentro" (caso especial para
        // cuando el caller quiere verificar "puedo leer este dir").
        Assert.True(PathTraversalGuard.TryResolveInside(_root, _root, out var resolved));
        Assert.Equal(_root, resolved);
    }

    [Fact]
    public void TryResolveInside_EmptyPath_ReturnsFalse()
    {
        Assert.False(PathTraversalGuard.TryResolveInside(_root, "", out _));
    }

    [Fact]
    public void TryResolveInside_EmptyRoot_ReturnsFalse()
    {
        Assert.False(PathTraversalGuard.TryResolveInside("", "file.txt", out _));
    }

    [Fact]
    public void TryResolveInside_NullPath_ReturnsFalse()
    {
        // El parámetro es no-nullable en la firma, pero el caller
        // podría pasar null vía reflection o un binding raro. Validamos
        // el comportamiento defensivo.
        Assert.False(PathTraversalGuard.TryResolveInside(_root, null!, out _));
    }
}
