using System.Text.Json;

namespace Basera.Shared.Files;

/// <summary>Bound from <c>Files</c>. A relative root is under the app's content root, never under wwwroot.</summary>
public sealed class FileStorageOptions
{
    public string LocalRoot { get; set; } = "App_Data/files";
}

/// <summary>
/// <see cref="IFileStorage"/> on a local folder, for development and single-server setups. The content type is
/// kept in a small <c>{id}.meta.json</c> next to each file. Writes go to a temporary file first and are moved
/// into place, so a reader never sees half a file.
/// </summary>
public sealed class LocalDiskFileStorage : IFileStorage
{
    private readonly string root;

    public LocalDiskFileStorage(string root)
    {
        this.root = Path.GetFullPath(root);
        Directory.CreateDirectory(this.root);
    }

    public string Root => root;

    public async Task<StoredFile> SaveAsync(FileKey key, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var path = FullPath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";

        long length;
        await using (var file = File.Create(temporary))
        {
            await content.CopyToAsync(file, cancellationToken);
            length = file.Length;
        }

        File.Move(temporary, path, overwrite: true);
        await File.WriteAllTextAsync(MetaPath(path), JsonSerializer.Serialize(new Meta(contentType)), cancellationToken);
        return new StoredFile(key, contentType, length);
    }

    public async Task<StoredFileContent?> OpenReadAsync(FileKey key, CancellationToken cancellationToken = default)
    {
        var path = FullPath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        var meta = File.Exists(MetaPath(path))
            ? JsonSerializer.Deserialize<Meta>(await File.ReadAllTextAsync(MetaPath(path), cancellationToken))
            : null;
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return new StoredFileContent(stream, meta?.ContentType ?? "application/octet-stream", stream.Length);
    }

    public Task<bool> ExistsAsync(FileKey key, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(FullPath(key)));

    public Task DeleteAsync(FileKey key, CancellationToken cancellationToken = default)
    {
        var path = FullPath(key);
        File.Delete(path);
        File.Delete(MetaPath(path));
        return Task.CompletedTask;
    }

    private string FullPath(FileKey key)
    {
        var path = Path.GetFullPath(Path.Combine(root, key.Path));

        // FileKey already guarantees this; checked again because it is the one thing that must never fail.
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"File key {key} resolves outside the storage root.");
        }

        return path;
    }

    private static string MetaPath(string path) => path + ".meta.json";

    private sealed record Meta(string ContentType);
}
