namespace EngineeringManager.Application.Development;

public interface IDevelopmentSampleDataSeeder
{
    Task SeedAsync(string environmentName, string contentRootPath, CancellationToken token);
}
