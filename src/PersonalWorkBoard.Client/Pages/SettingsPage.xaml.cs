using PersonalWorkBoard.Client.Services;

namespace PersonalWorkBoard.Client.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly VoiceService _voice;
    public SettingsPage(ApiClient api, VoiceService voice)
    {
        InitializeComponent();
        _api = api;
        _voice = voice;
        SpeechEndpoint.Text = voice.Endpoint;
        MorningPicker.Time = TimeSpan.Parse(Preferences.Default.Get("morning_reminder", "09:00"));
        EveningPicker.Time = TimeSpan.Parse(Preferences.Default.Get("evening_reminder", "20:30"));
    }

    private async void OnSaveSpeechClicked(object? sender, EventArgs e)
    {
        try { await _voice.SaveProviderAsync(SpeechEndpoint.Text ?? "", SpeechKey.Text ?? ""); SpeechKey.Text = ""; MessageLabel.Text = "转写设置已保存。 "; }
        catch (Exception ex) { MessageLabel.Text = ex.Message; }
    }

    private void OnSaveReminderClicked(object? sender, EventArgs e)
    {
        Preferences.Default.Set("morning_reminder", MorningPicker.Time.ToString(@"hh\:mm"));
        Preferences.Default.Set("evening_reminder", EveningPicker.Time.ToString(@"hh\:mm"));
        MessageLabel.Text = "提醒时间已保存。 ";
    }

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        if (!await DisplayAlert("退出设备", "本机离线缓存会保留，但需要重新登录或扫码才能继续同步。", "退出", "取消")) return;
        await _api.LogoutAsync();
        ((App)Application.Current!).ShowLogin();
    }
}
