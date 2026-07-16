namespace EngineeringManager.Infrastructure.Files;

public sealed class LocalFileStore : IFileStore
{
    private readonly string rootDirectory;
    private readonly string rootPrefix;

    public LocalFileStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        this.rootDirectory = Path.GetFullPath(rootDirectory);
        rootPrefix = this.rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(this.rootDirectory);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ValidateLeafName(fileName, nameof(fileName));

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var storedPath = ResolveStoredPath(storedName);

        await using var output = new FileStream(
            storedPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await content.CopyToAsync(output, cancellationToken);
        return storedName;
    }

    public Task<Stream> OpenReadAsync(string storedName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var storedPath = ResolveStoredPath(storedName);
        Stream stream = new FileStream(
            storedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storedName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var storedPath = ResolveStoredPath(storedName);
        File.Delete(storedPath);
        return Task.CompletedTask;
    }

    private string ResolveStoredPath(string storedName)
    {
        ValidateLeafName(storedName, nameof(storedName));
        var storedPath = Path.GetFullPath(Path.Combine(rootDirectory, storedName));
        if (!storedPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Stored file path must remain inside the configured root directory.", nameof(storedName));
        }

        return storedPath;
    }

    private static void ValidateLeafName(string fileName, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName, parameterName);
        if (Path.IsPathRooted(fileName)
            || fileName.Contains("..", StringComparison.Ordinal)
            || fileName.Contains(Path.DirectorySeparatorChar)
            || fileName.Contains(Path.AltDirectorySeparatorChar)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("File name must be a safe leaf name without path segments.", parameterName);
        }
    }
}
