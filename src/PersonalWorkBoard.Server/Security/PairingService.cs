using Dapper;
using Microsoft.Extensions.Options;
using PersonalWorkBoard.Contracts;
using PersonalWorkBoard.Server.Database;
using PersonalWorkBoard.Server.Options;

namespace PersonalWorkBoard.Server.Security;

public sealed class PairingService(
    DbConnectionFactory connections,
    AuthService auth,
    IOptions<ServerOptions> options)
{
    private readonly ServerOptions _options = options.Value;

    public async Task<PairingTicketResponse> CreateAsync(AuthPrincipal principal, CancellationToken cancellationToken)
    {
        var ticket = TokenUtility.CreateToken();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(_options.PairingMinutes, 1, 15));
        await using var connection = connections.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO pairing_tickets (id, owner_id, pc_device_id, ticket_hash, expires_at, created_at)
            VALUES (@Id, @OwnerId, @DeviceId, @Hash, @ExpiresAt, @Now)
            """,
            new { Id = Guid.NewGuid(), OwnerId = principal.UserId, DeviceId = principal.DeviceId, Hash = TokenUtility.Hash(ticket), ExpiresAt = expiresAt.UtcDateTime, Now = DateTime.UtcNow },
            cancellationToken: cancellationToken));

        var serverUrl = _options.PublicBaseUrl.TrimEnd('/');
        var payload = $"pwb://pair?server={Uri.EscapeDataString(serverUrl)}&ticket={Uri.EscapeDataString(ticket)}";
        return new PairingTicketResponse(ticket, payload, expiresAt);
    }

    public async Task<RedeemPairingTicketResponse?> RedeemAsync(RedeemPairingTicketRequest request, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<TicketRow>(new CommandDefinition(
            """
            SELECT id, owner_id FROM pairing_tickets
            WHERE ticket_hash = @Hash AND used_at IS NULL AND expires_at > UTC_TIMESTAMP(6)
            FOR UPDATE
            """,
            new { Hash = TokenUtility.Hash(request.Ticket) }, transaction, cancellationToken: cancellationToken));
        if (row is null) return null;

        var deviceId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO devices (id, owner_id, name, platform, last_seen_at, created_at)
            VALUES (@DeviceId, @OwnerId, @Name, @Platform, @Now, @Now);
            UPDATE pairing_tickets SET used_at = @Now WHERE id = @TicketId;
            """,
            new { DeviceId = deviceId, OwnerId = row.OwnerId, Name = request.DeviceName[..Math.Min(120, request.DeviceName.Length)], Platform = request.DevicePlatform[..Math.Min(40, request.DevicePlatform.Length)], Now = now, TicketId = row.Id },
            transaction, cancellationToken: cancellationToken));
        var session = await auth.CreateSessionAsync(connection, row.OwnerId, deviceId, cancellationToken, transaction);
        await transaction.CommitAsync(cancellationToken);
        return new RedeemPairingTicketResponse(session.Token, session.ExpiresAt, row.OwnerId, deviceId, _options.Name);
    }

    private sealed record TicketRow(Guid Id, Guid OwnerId);
}
