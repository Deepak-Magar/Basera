using System.Text.RegularExpressions;

namespace Basera.Shared.Files;

/// <summary>
/// Private file storage for ID photos, payment slips and PDFs (ARCHITECTURE §5). Files are never served by URL;
/// a controller that has checked authorization streams them. Local disk in development, S3-compatible object
/// storage in production (to follow), and an encrypting adapter for ID documents (with Compliance).
/// </summary>
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(FileKey key, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Null when there is no such file.</summary>
    Task<StoredFileContent?> OpenReadAsync(FileKey key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(FileKey key, CancellationToken cancellationToken = default);

    Task DeleteAsync(FileKey key, CancellationToken cancellationToken = default);
}

/// <summary>
/// Where a file lives: <c>tenant/{tenantId}/{module}/{id}</c>. Built from typed parts, so a caller can never
/// smuggle in "../" or another tenant's folder through a string.
/// </summary>
public sealed partial record FileKey
{
    public FileKey(Guid tenantId, string module, Guid id)
    {
        if (tenantId == Guid.Empty || id == Guid.Empty)
        {
            throw new ArgumentException("Tenant and file ids must not be empty.");
        }

        if (!ModuleName().IsMatch(module))
        {
            throw new ArgumentException($"Module '{module}' must be lower-case letters, e.g. \"residents\".", nameof(module));
        }

        TenantId = tenantId;
        Module = module;
        Id = id;
    }

    public Guid TenantId { get; }

    public string Module { get; }

    public Guid Id { get; }

    public string Path => $"tenant/{TenantId:D}/{Module}/{Id:D}";

    public override string ToString() => Path;

    [GeneratedRegex("^[a-z]{2,32}$")]
    private static partial Regex ModuleName();
}

public sealed record StoredFile(FileKey Key, string ContentType, long Length);

/// <summary>The caller disposes <see cref="Content"/>.</summary>
public sealed record StoredFileContent(Stream Content, string ContentType, long Length);
