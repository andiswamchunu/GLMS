using System.Text;
using GLMS.Tests.Helpers;
using GLMS.Web.Services;

namespace GLMS.Tests;

/// <summary>Tests the "simulated file server" against a real temporary folder.</summary>
public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "glms-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_CreatesTheStorageFolder()
    {
        _ = new LocalFileStorage(_root);

        Assert.True(Directory.Exists(_root));
    }

    [Fact]
    public async Task SaveAsync_WritesTheFileUnderAGeneratedPdfName()
    {
        var storage = new LocalFileStorage(_root);
        using var content = PdfSamples.ValidPdfStream();

        var name = await storage.SaveAsync(content);

        Assert.EndsWith(".pdf", name);
        Assert.Equal(PdfSamples.ValidPdf(), await File.ReadAllBytesAsync(Path.Combine(_root, name)));
    }

    [Fact]
    public async Task SaveAsync_TwoUploads_GetDifferentNames()
    {
        var storage = new LocalFileStorage(_root);

        string first, second;
        using (var a = PdfSamples.ValidPdfStream()) first = await storage.SaveAsync(a);
        using (var b = PdfSamples.ValidPdfStream()) second = await storage.SaveAsync(b);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task OpenRead_ReturnsTheSavedBytes()
    {
        var storage = new LocalFileStorage(_root);
        string name;
        using (var content = PdfSamples.ValidPdfStream()) name = await storage.SaveAsync(content);

        await using var stream = storage.OpenRead(name);

        Assert.NotNull(stream);
        using var reader = new MemoryStream();
        await stream!.CopyToAsync(reader);
        Assert.Equal(Encoding.ASCII.GetString(PdfSamples.ValidPdf()), Encoding.ASCII.GetString(reader.ToArray()));
    }

    [Fact]
    public void OpenRead_MissingFile_ReturnsNull()
    {
        var storage = new LocalFileStorage(_root);

        Assert.Null(storage.OpenRead("does-not-exist.pdf"));
    }

    [Fact]
    public async Task Delete_RemovesTheFile_AndReportsWhetherItExisted()
    {
        var storage = new LocalFileStorage(_root);
        string name;
        using (var content = PdfSamples.ValidPdfStream()) name = await storage.SaveAsync(content);

        Assert.True(storage.Delete(name));
        Assert.False(File.Exists(Path.Combine(_root, name)));
        Assert.False(storage.Delete(name));
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("sub/dir/file.pdf")]
    [InlineData("")]
    [InlineData("   ")]
    public void PathTraversalNames_AreNeverResolved(string name)
    {
        var storage = new LocalFileStorage(_root);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(_root)!, "secret.txt"), "secret");

        try
        {
            Assert.Null(storage.OpenRead(name));
            Assert.False(storage.Delete(name));
            Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(_root)!, "secret.txt")));
        }
        finally
        {
            File.Delete(Path.Combine(Path.GetDirectoryName(_root)!, "secret.txt"));
        }
    }
}
