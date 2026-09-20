using MySqlConnector;

namespace PersonalWorkBoard.Server.Database;

public sealed class DbConnectionFactory(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("Main")
        ?? throw new InvalidOperationException("缺少 ConnectionStrings:Main。 ");

    public MySqlConnection Create() => new(_connectionString);
}
