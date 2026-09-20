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
    private string _newTaskTitle = string.Empty;
    private string _newTaskPriority = "Medium";
    private string _newTaskMinutes = "30";
    private string _taskMessage = string.Empty;

    public ObservableCollection<LocalWorkTask> TodayTasks { get; } = [];
    public int ActiveProjects { get => _activeProjects; private set => SetProperty(ref _activeProjects, value); }
    public int CompletedToday { get => _completedToday; private set => SetProperty(ref _completedToday, value); }
    public int WaitingItems { get => _waitingItems; private set => SetProperty(ref _waitingItems, value); }
    public string SyncStatus { get => _syncStatus; private set => SetProperty(ref _syncStatus, value); }
    public string NewTaskTitle { get => _newTaskTitle; set => SetProperty(ref _newTaskTitle, value); }
    public string NewTaskPriority { get => _newTaskPriority; set => SetProperty(ref _newTaskPriority, value); }
    public string NewTaskMinutes { get => _newTaskMinutes; set => SetProperty(ref _newTaskMinutes, value); }
    public string TaskMessage { get => _taskMessage; private set => SetProperty(ref _taskMessage, value); }
    public IReadOnlyList<string> Priorities { get; } = ["High", "Medium", "Low"];
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public IAsyncRelayCommand RefreshCommand => new AsyncRelayCommand(RefreshAsync);
    public IAsyncRelayCommand AddTaskCommand => new AsyncRelayCommand(AddTaskAsync);

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
    private async Task AddTaskAsync()
    {
        var title = NewTaskTitle.Trim();
        if (title.Length == 0)
        {
            TaskMessage = "请先填写今天要做的事情。";
            return;
        }

        var minutes = int.TryParse(NewTaskMinutes, out var parsed) ? Math.Clamp(parsed, 5, 1440) : 30;
        var task = new LocalWorkTask
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Status = "Todo",
            Priority = NewTaskPriority,
            PlannedDate = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
            EstimatedMinutes = minutes,
            RowVersion = 0
        };
        await sync.QueueTaskAsync(task);
        TodayTasks.Insert(0, task);
        NewTaskTitle = string.Empty;
        NewTaskMinutes = "30";
        TaskMessage = "已加入今日工作，恢复局域网后会自动同步。";
        try { await sync.SyncNowAsync(); } catch { }
    }
}
