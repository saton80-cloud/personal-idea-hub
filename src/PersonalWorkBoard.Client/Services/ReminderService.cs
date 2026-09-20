namespace PersonalWorkBoard.Client.Services;

public sealed class ReminderService(LocalStore store)
{
    private CancellationTokenSource? _source;

    public void Start()
    {
        if (_source is not null) return;
        _source = new CancellationTokenSource();
        _ = RunAsync(_source.Token);
    }

    public void Stop()
    {
        _source?.Cancel();
        _source = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var now = DateTime.Now;
            var morning = Preferences.Default.Get("morning_reminder", "09:00");
            var evening = Preferences.Default.Get("evening_reminder", "20:30");
            var current = now.ToString("HH:mm");
            if (current == morning) await ShowOnceAsync("morning", "今日计划", "查看今天的任务，并确定最重要的一件事。 ", now);
            if (current == evening) await ShowOnceAsync("evening", "每日复盘", "记录今天的进展、阻塞问题和明天第一步。 ", now);
        }
    }

    private async Task ShowOnceAsync(string kind, string title, string message, DateTime now)
    {
        var key = $"reminder_{kind}_{now:yyyyMMdd}";
        if (await store.GetStateAsync(key) == "shown") return;
        await store.SetStateAsync(key, "shown");
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (Application.Current?.Windows.FirstOrDefault()?.Page is Page page)
                await page.DisplayAlertAsync(title, message, "知道了");
        });
    }
}
