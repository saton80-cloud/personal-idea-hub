using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersonalWorkBoard.Client.Services;

namespace PersonalWorkBoard.Client.ViewModels;

public sealed class LoginViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private string _serverUrl = "http://192.168.1.100:5088";
    private string _userName = "admin";
    private string _password = string.Empty;
    private string _message = "PC首次登录后，可在同步中心生成二维码供手机扫描。 ";
    private bool _isBusy;

    public LoginViewModel(ApiClient api)
    {
        _api = api;
        LoginCommand = new AsyncRelayCommand(LoginAsync, () => !IsBusy);
    }

    public string ServerUrl { get => _serverUrl; set => SetProperty(ref _serverUrl, value); }
    public string UserName { get => _userName; set => SetProperty(ref _userName, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string Message { get => _message; set => SetProperty(ref _message, value); }
    public bool IsBusy { get => _isBusy; set { if (SetProperty(ref _isBusy, value)) LoginCommand.NotifyCanExecuteChanged(); } }
    public AsyncRelayCommand LoginCommand { get; }

    private async Task LoginAsync()
    {
        IsBusy = true;
        try
        {
            await _api.LoginAsync(ServerUrl, UserName, Password);
            ((App)Application.Current!).ShowWorkspace();
        }
        catch (Exception ex)
        {
            Message = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
