using PersonalWorkBoard.Client.ViewModels;

namespace PersonalWorkBoard.Client.Pages;

public partial class SyncPage : ContentPage
{
    private readonly SyncViewModel _viewModel;
    private readonly IServiceProvider _services;
    public SyncPage(SyncViewModel viewModel, IServiceProvider services) { InitializeComponent(); BindingContext = _viewModel = viewModel; _services = services; }
    protected override async void OnAppearing() { base.OnAppearing(); await _viewModel.LoadAsync(); }
    private async void OnScanClicked(object? sender, EventArgs e) => await Navigation.PushAsync(_services.GetRequiredService<QrScannerPage>());
}
