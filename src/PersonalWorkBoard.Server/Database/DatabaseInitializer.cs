using System.Reflection;
using Dapper;

namespace PersonalWorkBoard.Server.Database;

public sealed class DatabaseInitializer(DbConnectionFactory connections)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "PersonalWorkBoard.Server.Database.schema.sql";
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"找不到数据库资源 {resourceName}");
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync(cancellationToken);
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }
}
