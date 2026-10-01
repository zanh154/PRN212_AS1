namespace AssignmentPRN.DataAccess.Services;

/// <summary>Stores course-material bytes under the configured web root.</summary>
public sealed class LocalMaterialFileStore(string webRootPath) : IMaterialFileStore
{
    private const string MaterialFolder = "materials";
    private readonly string root = Path.GetFullPath(
        string.IsNullOrWhiteSpace(webRootPath)
            ? throw new ArgumentException("A web-root path is required.", nameof(webRootPath))
            : webRootPath);

    public async Task<string> SaveAsync(
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var folder = Path.Combine(root, MaterialFolder);
        Directory.CreateDirectory(folder);

        var extension = Path.GetExtension(Path.GetFileName(originalFileName));
        var name = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folder, name);

        await using var target = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await content.CopyToAsync(target, cancellationToken);

        return $"/{MaterialFolder}/{name}";
    }

    public Task<Stream?> OpenReadAsync(
        string storedPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Resolve(storedPath);
        Stream? stream = fullPath is not null && File.Exists(fullPath)
            ? new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        string? storedPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Resolve(storedPath);
        if (fullPath is not null && File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string? Resolve(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        var relative = storedPath.TrimStart('/', '\\')
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(root, relative));
        var materialRoot = Path.GetFullPath(Path.Combine(root, MaterialFolder))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(materialRoot, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : null;
    }
}
