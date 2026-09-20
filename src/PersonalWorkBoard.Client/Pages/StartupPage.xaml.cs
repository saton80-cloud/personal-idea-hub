using PersonalWorkBoard.Client.Services;

namespace PersonalWorkBoard.Client.Pages;

public partial class StartupPage : ContentPage
{
    private readonly ApiClient _api;
    private bool _started;

    public StartupPage(ApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_started) return;
        _started = true;
        await Task.Delay(250);
        if (await _api.HasSessionAsync()) ((App)Application.Current!).ShowWorkspace();
        else ((App)Application.Current!).ShowLogin();
    }
}
