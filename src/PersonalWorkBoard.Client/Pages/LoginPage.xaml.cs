using PersonalWorkBoard.Client.ViewModels;
using PersonalWorkBoard.Client.Services;

namespace PersonalWorkBoard.Client.Pages;

public partial class LoginPage : ContentPage
{
    private readonly IServiceProvider _services;
    private readonly ApiClient _api;

    public LoginPage(LoginViewModel viewModel, IServiceProvider services, ApiClient api)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _services = services;
        _api = api;
    }

    private async void OnScanClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<QrScannerPage>());

    private async void OnOfflineClicked(object? sender, EventArgs e)
    {
        await _api.EnableOfflineModeAsync();
        ((App)Application.Current!).ShowWorkspace();
    }
}
