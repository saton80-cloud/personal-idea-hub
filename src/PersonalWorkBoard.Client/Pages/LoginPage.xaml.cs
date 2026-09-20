using PersonalWorkBoard.Client.ViewModels;

namespace PersonalWorkBoard.Client.Pages;

public partial class LoginPage : ContentPage
{
    private readonly IServiceProvider _services;

    public LoginPage(LoginViewModel viewModel, IServiceProvider services)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _services = services;
    }

    private async void OnScanClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(_services.GetRequiredService<QrScannerPage>());
}
