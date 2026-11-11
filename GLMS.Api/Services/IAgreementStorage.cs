namespace GLMS.Api.Services;

/// <summary>Abstraction over the "file server" that stores signed agreements.</summary>
public interface IAgreementStorage
{
    /// <summary>Stores the content and returns the generated (GUID based) stored file name.</summary>
    Task<string> SaveAsync(Stream content, CancellationToken ct = default);

    /// <summary>Opens the stored file for reading, or returns null if it does not exist.</summary>
    Stream? OpenRead(string storedName);

    /// <summary>Deletes the stored file. Returns false if it did not exist.</summary>
    bool Delete(string storedName);
}
