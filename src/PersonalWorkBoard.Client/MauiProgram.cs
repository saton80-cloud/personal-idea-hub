using PersonalWorkBoard.Client.Pages;
using PersonalWorkBoard.Client.Services;
using PersonalWorkBoard.Client.ViewModels;
using ZXing.Net.Maui.Controls;

namespace PersonalWorkBoard.Client;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().UseBarcodeReader();
        builder.Services.AddSingleton<LocalStore>();
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<SyncService>();
        builder.Services.AddSingleton<ReminderService>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<StartupPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<QrScannerPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<BoardPage>();
        builder.Services.AddTransient<SyncPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<BoardViewModel>();
        builder.Services.AddTransient<SyncViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        return builder.Build();
    }
}
