using Microsoft.Data.SqlClient;

namespace EngineeringManager.Infrastructure.Backups;

public sealed class SqlServerBackupExecutor(string connectionString) : IDatabaseBackupExecutor
{
    public async Task ExecuteAsync(string destinationPath, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            throw new InvalidOperationException("数据库连接字符串没有指定数据库名。");
        }

        var databaseName = builder.InitialCatalog.Replace("]", "]]", StringComparison.Ordinal);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 300;
        command.CommandText = $"BACKUP DATABASE [{databaseName}] TO DISK = @destination WITH INIT, CHECKSUM";
        command.Parameters.AddWithValue("@destination", destinationPath);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
