using PersonalWorkBoard.Client.Pages;
using PersonalWorkBoard.Client.Services;

namespace PersonalWorkBoard.Client;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly SyncService _sync;
    private readonly ApiClient _api;

    public App(IServiceProvider services, SyncService sync, ApiClient api)
    {
        InitializeComponent();
        _services = services;
        _sync = sync;
        _api = api;
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var startup = _services.GetRequiredService<StartupPage>();
        var window = new Window(new NavigationPage(startup)) { Title = "个人工作看板" };
        window.Resumed += async (_, _) => await TrySyncAsync();
        return window;
    }

    public void ShowWorkspace()
    {
        Windows[0].Page = _services.GetRequiredService<AppShell>();
        _services.GetRequiredService<ReminderService>().Start();
        _ = TrySyncAsync();
    }

    public void ShowLogin() => Windows[0].Page = new NavigationPage(_services.GetRequiredService<LoginPage>());

    private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess != NetworkAccess.None) await TrySyncAsync();
    }

    private async Task TrySyncAsync()
    {
        try
        {
            if (await _api.HasSessionAsync()) await _sync.SyncNowAsync();
        }
        catch
        {
            // Offline is a normal state; queued mutations remain in local storage.
        }
    }
}
