using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalWorkBoard.Client.Models;
using PersonalWorkBoard.Client.Services;

namespace PersonalWorkBoard.Client.ViewModels;

public sealed class BoardViewModel(LocalStore store, SyncService sync) : ObservableObject
{
    private string _newTitle = string.Empty;
    private string _newDescription = string.Empty;
    private string _selectedType = "General";
    private bool _isBusy;
    private string _message = "支持代码、网站、产品创意、新品开发和日常工作。 ";

    public ObservableCollection<LocalWorkItem> Items { get; } = [];
    public IReadOnlyList<string> Types { get; } = ["Code", "Website", "ProductIdea", "NewProduct", "DailyWork", "General"];
    public string NewTitle { get => _newTitle; set => SetProperty(ref _newTitle, value); }
    public string NewDescription { get => _newDescription; set => SetProperty(ref _newDescription, value); }
    public string SelectedType { get => _selectedType; set => SetProperty(ref _selectedType, value); }
    public string Message { get => _message; set => SetProperty(ref _message, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public IAsyncRelayCommand AddCommand => new AsyncRelayCommand(AddAsync);
    public IAsyncRelayCommand RefreshCommand => new AsyncRelayCommand(RefreshAsync);

    public async Task RefreshAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            try { await sync.SyncNowAsync(); } catch { Message = "离线模式：本机记录将在恢复局域网后自动同步。 "; }
            Items.Clear();
            foreach (var item in await store.GetWorkItemsAsync()) Items.Add(item);
        }
        finally { IsBusy = false; }
    }

    private async Task AddAsync()
    {
        var title = NewTitle.Trim();
        if (title.Length == 0) { Message = "请先填写工作或项目标题。 "; return; }
        var item = new LocalWorkItem
        {
            Id = Guid.NewGuid().ToString(),
            Type = SelectedType,
            Status = "Inbox",
            Priority = "Medium",
            Title = title,
            Description = NewDescription.Trim(),
            RowVersion = 0
        };
        await sync.QueueWorkItemAsync(item);
        Items.Insert(0, item);
        NewTitle = string.Empty;
        NewDescription = string.Empty;
        Message = "已保存到本机收件箱，联网后自动同步。 ";
        try { await sync.SyncNowAsync(); } catch { }
    }
}
