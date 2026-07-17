using System.Text;
using EngineeringManager.Application.Development;
using EngineeringManager.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EngineeringManager.Infrastructure.Development;

public sealed class DevelopmentSampleDataSeeder(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : IDevelopmentSampleDataSeeder
{
    public const string AdministratorUserName = "test-admin";
    private static int passwordSequence = Random.Shared.Next(1000, 9000);

    public static string GenerateTestPassword() => $"TestAdmin{Interlocked.Increment(ref passwordSequence) % 10000:0000}";

    public static void ValidateSafety(string environmentName, string databaseName)
    {
        if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) || !databaseName.EndsWith("_Test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("样例数据只能写入 Development 环境中明确以 _Test 结尾的测试数据库。");
    }

    public async Task SeedAsync(string environmentName, string contentRootPath, CancellationToken token)
    {
        var databaseName = db.Database.GetDbConnection().Database;
        ValidateSafety(environmentName, databaseName);
        var context = await new SampleDataBuilder(db, userManager, TimeProvider.System).BuildCoreAsync(token);
        var appData = Path.Combine(contentRootPath, "App_Data"); Directory.CreateDirectory(appData);
        var lines = new List<string>
        {
            "测试环境专用账号（禁止用于生产）",
            $"数据库：{databaseName}",
            string.Empty
        };
        foreach (var item in context.Credentials)
        {
            lines.Add($"{item.DisplayName}（{item.Role}）");
            lines.Add($"用户名：{item.UserName}");
            lines.Add($"密码：{item.Password}");
            lines.Add(string.Empty);
        }
        var credentials = string.Join(Environment.NewLine, lines);
        await File.WriteAllTextAsync(Path.Combine(appData, "local-test-credentials.txt"), credentials, new UTF8Encoding(false), token);
    }
}
