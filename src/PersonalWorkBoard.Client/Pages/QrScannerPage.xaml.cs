using PersonalWorkBoard.Client.Services;
using ZXing.Net.Maui;

namespace PersonalWorkBoard.Client.Pages;

public partial class QrScannerPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly SyncService _sync;
    private int _processing;

    public QrScannerPage(ApiClient api, SyncService sync)
    {
        InitializeComponent();
        _api = api;
        _sync = sync;
        CameraView.Options = new BarcodeReaderOptions { Formats = BarcodeFormats.TwoDimensional, AutoRotate = true, Multiple = false };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted) StatusLabel.Text = "需要相机权限才能扫描二维码。 ";
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value) || Interlocked.Exchange(ref _processing, 1) == 1) return;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                CameraView.IsDetecting = false;
                StatusLabel.Text = "正在安全配对并同步数据…";
                await _api.RedeemPairingAsync(value);
                try { await _sync.SyncNowAsync(); }
                catch { /* Pairing succeeded; the workspace will keep the local queue and retry. */ }
                ((App)Application.Current!).ShowWorkspace();
            }
            catch (Exception ex)
            {
                StatusLabel.Text = ex.Message;
                CameraView.IsDetecting = true;
                Interlocked.Exchange(ref _processing, 0);
            }
        });
    }
}
