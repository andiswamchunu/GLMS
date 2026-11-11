namespace GLMS.Api.Services;

/// <summary>
/// Simulates a file server by writing to a folder on disk (App_Data/SignedAgreements by default).
/// Files are stored under server-generated names, so user supplied names never touch the file system.
/// </summary>
public sealed class LocalFileStorage : IAgreementStorage
{
    private readonly string _root;

    public LocalFileStorage(string rootPath)
    {
        _root = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, CancellationToken ct = default)
    {
        var storedName = $"{Guid.NewGuid():N}.pdf";
        var path = Path.Combine(_root, storedName);

        await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(target, ct);
        return storedName;
    }

    public Stream? OpenRead(string storedName)
    {
        var path = ResolveSafePath(storedName);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
    }

    public bool Delete(string storedName)
    {
        var path = ResolveSafePath(storedName);
        if (path is null || !File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    private string? ResolveSafePath(string storedName)
    {
        if (string.IsNullOrWhiteSpace(storedName) || Path.GetFileName(storedName) != storedName)
        {
            return null; // rejects "..\\x", "a/b", rooted paths, etc.
        }

        return Path.Combine(_root, storedName);
    }
}
