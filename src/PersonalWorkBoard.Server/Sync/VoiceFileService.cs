using Dapper;
using PersonalWorkBoard.Server.Database;

namespace PersonalWorkBoard.Server.Sync;

public sealed class VoiceFileService(DbConnectionFactory connections, IConfiguration configuration)
{
    private readonly string _directory = configuration["Voice:DataDirectory"] ?? "/var/lib/personal-work-board/voice";

    public async Task<bool> SaveAsync(Guid ownerId, Guid taskId, Guid id, Stream input, CancellationToken ct)
    {
        await using var connection = connections.Create();
        var owned = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM work_tasks WHERE id=@taskId AND owner_id=@ownerId AND deleted_at IS NULL",
            new { taskId, ownerId }, cancellationToken: ct));
        if (owned == 0) return false;
        var existing = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT task_id FROM voice_notes WHERE id=@id AND owner_id=@ownerId", new { id, ownerId }, cancellationToken: ct));
        if (existing is not null) return existing == taskId;
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, id.ToString("N") + ".m4a");
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var output = File.Create(temp))
            {
                await input.CopyToAsync(output, ct);
                if (output.Length is <= 0 or > 25 * 1024 * 1024) throw new InvalidOperationException("录音超出大小限制。 ");
            }
            File.Move(temp, path, overwrite: true);
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO voice_notes (id,owner_id,task_id,file_size,created_at) VALUES (@id,@ownerId,@taskId,@size,UTC_TIMESTAMP(6))",
                new { id, ownerId, taskId, size = new FileInfo(path).Length }, cancellationToken: ct));
            return true;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public async Task<Guid?> FindByTaskAsync(Guid ownerId, Guid taskId, CancellationToken ct)
    {
        await using var connection = connections.Create();
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM voice_notes WHERE owner_id=@ownerId AND task_id=@taskId ORDER BY created_at DESC LIMIT 1",
            new { ownerId, taskId }, cancellationToken: ct));
    }

    public async Task<string?> GetPathAsync(Guid ownerId, Guid id, CancellationToken ct)
    {
        await using var connection = connections.Create();
        var owned = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM voice_notes WHERE id=@id AND owner_id=@ownerId",
            new { id, ownerId }, cancellationToken: ct));
        var path = Path.Combine(_directory, id.ToString("N") + ".m4a");
        return owned > 0 && File.Exists(path) ? path : null;
    }
}
