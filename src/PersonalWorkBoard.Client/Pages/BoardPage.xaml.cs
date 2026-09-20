using PersonalWorkBoard.Client.ViewModels;

namespace PersonalWorkBoard.Client.Pages;

public partial class BoardPage : ContentPage
{
    private readonly BoardViewModel _viewModel;
    public BoardPage(BoardViewModel viewModel) { InitializeComponent(); BindingContext = _viewModel = viewModel; }
    protected override async void OnAppearing() { base.OnAppearing(); await _viewModel.RefreshAsync(); }
}
