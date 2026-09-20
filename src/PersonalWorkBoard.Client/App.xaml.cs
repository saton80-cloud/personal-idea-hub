using PersonalWorkBoard.Client.Pages;
using PersonalWorkBoard.Client.Services;

namespace PersonalWorkBoard.Client;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var startup = _services.GetRequiredService<StartupPage>();
        return new Window(new NavigationPage(startup)) { Title = "个人工作看板" };
    }

    public void ShowWorkspace()
    {
        Windows[0].Page = _services.GetRequiredService<AppShell>();
        _services.GetRequiredService<ReminderService>().Start();
    }

    public void ShowLogin() => Windows[0].Page = new NavigationPage(_services.GetRequiredService<LoginPage>());
}
