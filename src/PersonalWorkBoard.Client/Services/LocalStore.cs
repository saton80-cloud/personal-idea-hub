using PersonalWorkBoard.Client.Models;
using SQLite;

namespace PersonalWorkBoard.Client.Services;

public sealed class LocalStore
{
    private readonly SQLiteAsyncConnection _database = new(
        Path.Combine(FileSystem.AppDataDirectory, "personal-work-board.db3"),
        SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
    private Task? _initialization;

    public Task InitializeAsync() => _initialization ??= InitializeCoreAsync();

    private async Task InitializeCoreAsync()
    {
        await _database.CreateTableAsync<LocalWorkItem>();
        await _database.CreateTableAsync<LocalWorkTask>();
        await _database.CreateTableAsync<LocalPendingMutation>();
        await _database.CreateTableAsync<LocalClientState>();
        await _database.CreateTableAsync<LocalSyncConflict>();
    }

    public async Task<List<LocalWorkItem>> GetWorkItemsAsync()
    {
        await InitializeAsync();
        return await _database.Table<LocalWorkItem>().Where(x => x.DeletedAt == null).OrderByDescending(x => x.UpdatedAt).ToListAsync();
    }

    public async Task<List<LocalWorkTask>> GetTodayTasksAsync()
    {
        await InitializeAsync();
        var today = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
        return await _database.Table<LocalWorkTask>().Where(x => x.PlannedDate == today && x.DeletedAt == null).ToListAsync();
    }

    public async Task UpsertWorkItemAsync(LocalWorkItem item)
    {
        await InitializeAsync();
        await _database.InsertOrReplaceAsync(item);
    }

    public async Task UpsertTaskAsync(LocalWorkTask task)
    {
        await InitializeAsync();
        await _database.InsertOrReplaceAsync(task);
    }

    public async Task EnqueueAsync(LocalPendingMutation mutation)
    {
        await InitializeAsync();
        await _database.InsertOrReplaceAsync(mutation);
    }

    public async Task<List<LocalPendingMutation>> GetPendingAsync()
    {
        await InitializeAsync();
        return await _database.Table<LocalPendingMutation>().OrderBy(x => x.ClientChangedAt).Take(200).ToListAsync();
    }

    public async Task RemovePendingAsync(IEnumerable<Guid> mutationIds)
    {
        await InitializeAsync();
        foreach (var id in mutationIds) await _database.DeleteAsync<LocalPendingMutation>(id.ToString());
    }

    public async Task SaveConflictAsync(LocalSyncConflict conflict)
    {
        await InitializeAsync();
        await _database.InsertOrReplaceAsync(conflict);
    }

    public async Task<int> GetConflictCountAsync()
    {
        await InitializeAsync();
        return await _database.Table<LocalSyncConflict>().CountAsync();
    }

    public async Task ClearConflictsAsync()
    {
        await InitializeAsync();
        await _database.DeleteAllAsync<LocalSyncConflict>();
    }

    public async Task<string?> GetStateAsync(string key)
    {
        await InitializeAsync();
        return (await _database.FindAsync<LocalClientState>(key))?.Value;
    }

    public async Task SetStateAsync(string key, string value)
    {
        await InitializeAsync();
        await _database.InsertOrReplaceAsync(new LocalClientState { Key = key, Value = value });
    }
}
