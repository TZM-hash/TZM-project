using System.Text;
using EngineeringManager.Infrastructure.Files;
using FluentAssertions;

namespace EngineeringManager.Tests.Infrastructure;

public sealed class LocalFileStoreTests : IDisposable
{
    private readonly string rootDirectory = Path.Combine(Path.GetTempPath(), $"engineering-manager-files-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAndOpenReadPreservesContentWithoutOverwritingDuplicateNames()
    {
        var store = new LocalFileStore(rootDirectory);
        await using var first = new MemoryStream(Encoding.UTF8.GetBytes("第一份"));
        await using var second = new MemoryStream(Encoding.UTF8.GetBytes("第二份"));

        var firstName = await store.SaveAsync(first, "资料.pdf", CancellationToken.None);
        var secondName = await store.SaveAsync(second, "资料.pdf", CancellationToken.None);

        firstName.Should().NotBe(secondName);
        firstName.Should().EndWith(".pdf");
        secondName.Should().EndWith(".pdf");

        await using var stored = await store.OpenReadAsync(firstName, CancellationToken.None);
        using var reader = new StreamReader(stored, Encoding.UTF8);
        (await reader.ReadToEndAsync()).Should().Be("第一份");
    }

    [Theory]
    [InlineData("..\\secret.txt")]
    [InlineData("../secret.txt")]
    [InlineData("C:\\secret.txt")]
    [InlineData("folder/file.txt")]
    public async Task SaveRejectsUnsafeFileNames(string fileName)
    {
        var store = new LocalFileStore(rootDirectory);
        await using var content = new MemoryStream([1, 2, 3]);

        var action = () => store.SaveAsync(content, fileName, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteRemovesStoredFile()
    {
        var store = new LocalFileStore(rootDirectory);
        await using var content = new MemoryStream([1, 2, 3]);
        var storedName = await store.SaveAsync(content, "现场照片.jpg", CancellationToken.None);

        await store.DeleteAsync(storedName, CancellationToken.None);
        var action = () => store.OpenReadAsync(storedName, CancellationToken.None);

        await action.Should().ThrowAsync<FileNotFoundException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(rootDirectory))
        {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }
}
