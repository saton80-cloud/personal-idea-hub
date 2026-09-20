using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalWorkBoard.Client.Services;

namespace PersonalWorkBoard.Client.ViewModels;

public sealed class SyncViewModel(ApiClient api, SyncService sync, LocalStore store) : ObservableObject
{
    private string _qrPayload = string.Empty;
    private string _status = "PC和手机必须与服务器处于同一局域网。 ";
    private string _serverUrl = string.Empty;
    private int _conflictCount;
    private bool _isBusy;

    public string QrPayload { get => _qrPayload; private set => SetProperty(ref _qrPayload, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string ServerUrl { get => _serverUrl; private set => SetProperty(ref _serverUrl, value); }
    public int ConflictCount { get => _conflictCount; private set => SetProperty(ref _conflictCount, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public bool IsWindows => DeviceInfo.Platform == DevicePlatform.WinUI;
    public bool IsAndroid => DeviceInfo.Platform == DevicePlatform.Android;
    public IAsyncRelayCommand GenerateQrCommand => new AsyncRelayCommand(GenerateQrAsync);
    public IAsyncRelayCommand SyncNowCommand => new AsyncRelayCommand(SyncNowAsync);

    public async Task LoadAsync()
    {
        ServerUrl = await api.GetServerUrlAsync();
        ConflictCount = await store.GetConflictCountAsync();
    }

    private async Task GenerateQrAsync()
    {
        IsBusy = true;
        try
        {
            var ticket = await api.CreatePairingTicketAsync();
            QrPayload = ticket.QrPayload;
            Status = $"二维码将在 {ticket.ExpiresAt.ToLocalTime():HH:mm:ss} 失效，只能使用一次。 ";
        }
        catch (Exception ex) { Status = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task SyncNowAsync()
    {
        IsBusy = true;
        try
        {
            var result = await sync.SyncNowAsync();
            ConflictCount = await store.GetConflictCountAsync();
            Status = $"同步完成：上传 {result.Uploaded}，下载 {result.Downloaded}，冲突 {result.Conflicts}";
        }
        catch (Exception ex) { Status = $"同步失败：{ex.Message}"; }
        finally { IsBusy = false; }
    }
}
