using Dapper;
using Microsoft.Extensions.Options;
using PersonalWorkBoard.Contracts;
using PersonalWorkBoard.Server.Database;
using PersonalWorkBoard.Server.Options;

namespace PersonalWorkBoard.Server.Security;

public sealed record AuthPrincipal(Guid UserId, Guid DeviceId, string DisplayName);

public sealed class AuthService(
    DbConnectionFactory connections,
    IOptions<ServerOptions> serverOptions,
    IOptions<BootstrapOptions> bootstrapOptions)
{
    private readonly ServerOptions _server = serverOptions.Value;
    private readonly BootstrapOptions _bootstrap = bootstrapOptions.Value;

    public async Task EnsureAdminAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM users", cancellationToken: cancellationToken));
        if (count > 0) return;
        if (string.IsNullOrWhiteSpace(_bootstrap.AdminPassword) || _bootstrap.AdminPassword.Length < 8)
            throw new InvalidOperationException("首次启动必须通过 Bootstrap__AdminPassword 配置至少8位管理员密码。 ");

        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var (salt, hash) = PasswordHasher.Hash(_bootstrap.AdminPassword);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO users (id, user_name, display_name, password_salt, password_hash, created_at, updated_at)
            VALUES (@Id, @UserName, @DisplayName, @Salt, @Hash, @Now, @Now)
            """,
            new { Id = userId, UserName = _bootstrap.AdminUserName, DisplayName = _bootstrap.AdminDisplayName, Salt = salt, Hash = hash, Now = now },
            cancellationToken: cancellationToken));
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        var user = await connection.QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition(
            "SELECT id, display_name, password_salt, password_hash FROM users WHERE user_name = @UserName LIMIT 1",
            new { request.UserName }, cancellationToken: cancellationToken));
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordSalt, user.PasswordHash)) return null;

        var deviceId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO devices (id, owner_id, name, platform, last_seen_at, created_at)
            VALUES (@DeviceId, @OwnerId, @Name, @Platform, @Now, @Now)
            """,
            new { DeviceId = deviceId, OwnerId = user.Id, Name = request.DeviceName[..Math.Min(120, request.DeviceName.Length)], Platform = request.DevicePlatform[..Math.Min(40, request.DevicePlatform.Length)], Now = now },
            cancellationToken: cancellationToken));

        var session = await CreateSessionAsync(connection, user.Id, deviceId, cancellationToken);
        return new LoginResponse(session.Token, session.ExpiresAt, user.Id, deviceId, user.DisplayName);
    }

    public async Task<AuthPrincipal?> AuthenticateAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var token = header[7..].Trim();
        if (token.Length < 32) return null;

        await using var connection = connections.Create();
        var principal = await connection.QuerySingleOrDefaultAsync<AuthPrincipal>(new CommandDefinition(
            """
            SELECT s.owner_id AS UserId, s.device_id AS DeviceId, u.display_name AS DisplayName
            FROM sessions s
            INNER JOIN users u ON u.id = s.owner_id
            INNER JOIN devices d ON d.id = s.device_id
            WHERE s.token_hash = @TokenHash AND s.revoked_at IS NULL AND s.expires_at > UTC_TIMESTAMP(6)
              AND d.revoked_at IS NULL
            LIMIT 1
            """,
            new { TokenHash = TokenUtility.Hash(token) }, cancellationToken: cancellationToken));
        if (principal is not null)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE devices SET last_seen_at = UTC_TIMESTAMP(6) WHERE id = @DeviceId",
                new { principal.DeviceId }, cancellationToken: cancellationToken));
        }
        return principal;
    }

    public async Task<(string Token, DateTimeOffset ExpiresAt)> CreateSessionAsync(
        System.Data.IDbConnection connection,
        Guid ownerId,
        Guid deviceId,
        CancellationToken cancellationToken,
        System.Data.IDbTransaction? transaction = null)
    {
        var token = TokenUtility.CreateToken();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(Math.Clamp(_server.SessionDays, 1, 365));
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO sessions (id, owner_id, device_id, token_hash, expires_at, created_at)
            VALUES (@Id, @OwnerId, @DeviceId, @TokenHash, @ExpiresAt, @Now)
            """,
            new { Id = Guid.NewGuid(), OwnerId = ownerId, DeviceId = deviceId, TokenHash = TokenUtility.Hash(token), ExpiresAt = expiresAt.UtcDateTime, Now = DateTime.UtcNow },
            transaction, cancellationToken: cancellationToken));
        return (token, expiresAt);
    }

    private sealed record UserRow(Guid Id, string DisplayName, byte[] PasswordSalt, byte[] PasswordHash);
}
