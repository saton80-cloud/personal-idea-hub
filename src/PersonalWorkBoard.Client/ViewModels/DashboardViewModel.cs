using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalWorkBoard.Client.Models;
using PersonalWorkBoard.Client.Services;

namespace PersonalWorkBoard.Client.ViewModels;

public sealed class DashboardViewModel(LocalStore store, SyncService sync, VoiceService voice) : ObservableObject
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
    private string _voiceText = string.Empty;
    private LocalVoiceNote? _selectedVoice;
    private bool _isRecording;

    public ObservableCollection<LocalWorkTask> TodayTasks { get; } = [];
    public ObservableCollection<LocalVoiceNote> VoiceDrafts { get; } = [];
    public int ActiveProjects { get => _activeProjects; private set => SetProperty(ref _activeProjects, value); }
    public int CompletedToday { get => _completedToday; private set => SetProperty(ref _completedToday, value); }
    public int WaitingItems { get => _waitingItems; private set => SetProperty(ref _waitingItems, value); }
    public string SyncStatus { get => _syncStatus; private set => SetProperty(ref _syncStatus, value); }
    public string NewTaskTitle { get => _newTaskTitle; set => SetProperty(ref _newTaskTitle, value); }
    public string NewTaskPriority { get => _newTaskPriority; set => SetProperty(ref _newTaskPriority, value); }
    public string NewTaskMinutes { get => _newTaskMinutes; set => SetProperty(ref _newTaskMinutes, value); }
    public string TaskMessage { get => _taskMessage; private set => SetProperty(ref _taskMessage, value); }
    public string VoiceText { get => _voiceText; set => SetProperty(ref _voiceText, value); }
    public bool IsRecording { get => _isRecording; private set => SetProperty(ref _isRecording, value); }
    public bool HasVoiceDraft { get => _selectedVoice is not null; }
    public IReadOnlyList<string> Priorities { get; } = ["High", "Medium", "Low"];
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public IAsyncRelayCommand RefreshCommand => new AsyncRelayCommand(RefreshAsync);
    public IAsyncRelayCommand AddTaskCommand => new AsyncRelayCommand(AddTaskAsync);
    public IAsyncRelayCommand StartVoiceCommand => new AsyncRelayCommand(StartVoiceAsync);
    public IAsyncRelayCommand StopVoiceCommand => new AsyncRelayCommand(StopVoiceAsync);
    public IAsyncRelayCommand TranscribeCommand => new AsyncRelayCommand(TranscribeAsync);
    public IRelayCommand<LocalVoiceNote> SelectVoiceCommand => new RelayCommand<LocalVoiceNote>(SelectVoice);
    public IAsyncRelayCommand<LocalWorkTask> PlayVoiceCommand => new AsyncRelayCommand<LocalWorkTask>(PlayVoiceAsync);

    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await TranscribePendingAsync();
            try
            {
                var result = await sync.SyncNowAsync();
                try { await voice.UploadPendingAsync(); } catch { /* retry when the LAN server is reachable */ }
                SyncStatus = $"刚刚同步 · 上传{result.Uploaded} 下载{result.Downloaded}";
            }
            catch
            {
                SyncStatus = "当前离线，修改会在恢复局域网后同步";
            }
            var items = await store.GetWorkItemsAsync();
            var tasks = await store.GetTodayTasksAsync();
            VoiceDrafts.Clear();
            foreach (var draft in (await store.GetVoiceNotesAsync()).Where(x => x.TaskId is null)) VoiceDrafts.Add(draft);
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
    public async Task TranscribePendingAsync()
    {
        if (!await voice.CanTranscribeAsync()) return;
        var pending = (await store.GetVoiceNotesAsync()).FirstOrDefault(x => x.TaskId is null && !x.Transcribed);
        if (pending is null) return;
        try
        {
            var text = await voice.TranscribeAsync(pending);
            if (_selectedVoice is null || _selectedVoice.Id == pending.Id)
            {
                SelectVoice(pending);
                VoiceText = text;
            }
            TaskMessage = "录音已在手机联网后转写，检查并修改文字再提交。";
        }
        catch (Exception ex) { TaskMessage = ex.Message; }
    }
    private async Task AddTaskAsync()
    {
        var transcript = VoiceText.Trim();
        var title = NewTaskTitle.Trim();
        if (title.Length == 0 && _selectedVoice is not null && transcript.Length > 0)
            title = transcript.Length > 80 ? transcript[..80] : transcript;
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
            Detail = _selectedVoice is null ? string.Empty : transcript,
            Status = "Todo",
            Priority = NewTaskPriority,
            PlannedDate = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
            EstimatedMinutes = minutes,
            RowVersion = 0
        };
        await sync.QueueTaskAsync(task);
        if (_selectedVoice is not null)
        {
            _selectedVoice.TaskId = task.Id;
            _selectedVoice.Transcript = transcript;
            await store.SaveVoiceNoteAsync(_selectedVoice);
            VoiceDrafts.Remove(_selectedVoice);
            _selectedVoice = null;
            OnPropertyChanged(nameof(HasVoiceDraft));
            VoiceText = string.Empty;
        }
        TodayTasks.Insert(0, task);
        NewTaskTitle = string.Empty;
        NewTaskMinutes = "30";
        TaskMessage = "已加入今日工作，恢复局域网后会自动同步。";
        try { await sync.SyncNowAsync(); await voice.UploadPendingAsync(); } catch { }
    }

    private async Task StartVoiceAsync()
    {
        try { await voice.StartAsync(); IsRecording = true; TaskMessage = "正在录音；结束后会先保存在手机。"; }
        catch (Exception ex) { TaskMessage = ex.Message; }
    }

    private async Task StopVoiceAsync()
    {
        try
        {
            var note = await voice.StopAsync();
            IsRecording = false;
            VoiceDrafts.Insert(0, note);
            SelectVoice(note);
            TaskMessage = "录音已离线保存，手机联网后可转为文字。";
        }
        catch (Exception ex) { IsRecording = false; TaskMessage = ex.Message; }
    }

    private void SelectVoice(LocalVoiceNote? note)
    {
        _selectedVoice = note;
        VoiceText = note?.Transcript ?? string.Empty;
        OnPropertyChanged(nameof(HasVoiceDraft));
        TaskMessage = note is null ? string.Empty : "可先转写，再修改下方文字，确认后加入今日任务。";
    }

    private async Task TranscribeAsync()
    {
        if (_selectedVoice is null) { TaskMessage = "请先录音或选择一段待处理录音。"; return; }
        try { TaskMessage = "正在联网转写…"; VoiceText = await voice.TranscribeAsync(_selectedVoice); TaskMessage = "文字已生成，可以修改后提交。"; }
        catch (Exception ex) { TaskMessage = ex.Message; }
    }

    private async Task PlayVoiceAsync(LocalWorkTask? task)
    {
        if (task is null) return;
        try
        {
            var path = await voice.GetOrDownloadAsync(task.Id);
            if (path is null) { TaskMessage = "这项任务还没有同步录音。"; return; }
            await Launcher.Default.OpenAsync(new OpenFileRequest("任务录音", new ReadOnlyFile(path)));
        }
        catch (Exception ex) { TaskMessage = $"打开录音失败：{ex.Message}"; }
    }
}
