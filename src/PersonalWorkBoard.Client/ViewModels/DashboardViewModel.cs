using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalWorkBoard.Client.Models;
using PersonalWorkBoard.Client.Services;

namespace PersonalWorkBoard.Client.ViewModels;

public sealed class DashboardViewModel(LocalStore store, SyncService sync) : ObservableObject
{
    private bool _isBusy;
    private int _activeProjects;
    private int _completedToday;
    private int _waitingItems;
    private string _syncStatus = "尚未同步";

    public ObservableCollection<LocalWorkTask> TodayTasks { get; } = [];
    public int ActiveProjects { get => _activeProjects; private set => SetProperty(ref _activeProjects, value); }
    public int CompletedToday { get => _completedToday; private set => SetProperty(ref _completedToday, value); }
    public int WaitingItems { get => _waitingItems; private set => SetProperty(ref _waitingItems, value); }
    public string SyncStatus { get => _syncStatus; private set => SetProperty(ref _syncStatus, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public IAsyncRelayCommand RefreshCommand => new AsyncRelayCommand(RefreshAsync);

    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            try
            {
                var result = await sync.SyncNowAsync();
                SyncStatus = $"刚刚同步 · 上传{result.Uploaded} 下载{result.Downloaded}";
            }
            catch
            {
                SyncStatus = "当前离线，修改会在恢复局域网后同步";
            }
            var items = await store.GetWorkItemsAsync();
            var tasks = await store.GetTodayTasksAsync();
            ActiveProjects = items.Count(x => x.Status is "Planned" or "InProgress" or "Waiting");
            WaitingItems = items.Count(x => x.Status == "Waiting");
            CompletedToday = tasks.Count(x => x.Status == "Done");
            TodayTasks.Clear();
            foreach (var task in tasks.OrderBy(x => x.Status == "Done").ThenByDescending(x => x.Priority)) TodayTasks.Add(task);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
