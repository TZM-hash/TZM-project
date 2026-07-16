namespace EngineeringManager.Infrastructure.Files;

public interface IFileStore
{
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string storedName, CancellationToken cancellationToken);

    Task DeleteAsync(string storedName, CancellationToken cancellationToken);
}
