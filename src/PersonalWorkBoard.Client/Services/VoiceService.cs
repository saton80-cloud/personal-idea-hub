using System.Net.Http.Headers;
using System.Text.Json;
using PersonalWorkBoard.Client.Models;

namespace PersonalWorkBoard.Client.Services;

public sealed class VoiceService(LocalStore store, ApiClient api)
{
    private const string KeyName = "speech_api_key";
    private const string EndpointName = "speech_endpoint";
    private const string DefaultEndpoint = "https://api.openai.com/v1/audio/transcriptions";
#if ANDROID
    private global::Android.Media.MediaRecorder? _recorder;
    private string? _recordingPath;
#endif

    public string Endpoint => Preferences.Default.Get(EndpointName, DefaultEndpoint);
    public async Task<bool> CanTranscribeAsync() =>
        Connectivity.Current.NetworkAccess == NetworkAccess.Internet &&
        !string.IsNullOrWhiteSpace(await SecureStorage.Default.GetAsync(KeyName));

    public async Task SaveProviderAsync(string endpoint, string key)
    {
        if (!Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("转写地址必须使用 HTTPS。 ");
        Preferences.Default.Set(EndpointName, uri.ToString());
        if (!string.IsNullOrWhiteSpace(key)) await SecureStorage.Default.SetAsync(KeyName, key.Trim());
    }

    public async Task StartAsync()
    {
#if ANDROID
        if (_recorder is not null) throw new InvalidOperationException("录音正在进行中。 ");
        if (await Permissions.RequestAsync<Permissions.Microphone>() != PermissionStatus.Granted)
            throw new InvalidOperationException("请允许麦克风权限，才能录入语音。 ");
        var folder = Path.Combine(FileSystem.AppDataDirectory, "voice");
        Directory.CreateDirectory(folder);
        _recordingPath = Path.Combine(folder, $"{Guid.NewGuid():N}.m4a");
        try
        {
            var recorder = OperatingSystem.IsAndroidVersionAtLeast(31)
                ? new global::Android.Media.MediaRecorder(Platform.AppContext)
                : new global::Android.Media.MediaRecorder();
            _recorder = recorder;
            recorder.SetAudioSource(global::Android.Media.AudioSource.Mic);
            recorder.SetOutputFormat(global::Android.Media.OutputFormat.Mpeg4);
            recorder.SetAudioEncoder(global::Android.Media.AudioEncoder.Aac);
            recorder.SetAudioSamplingRate(16000);
            recorder.SetOutputFile(_recordingPath);
            recorder.Prepare();
            recorder.Start();
        }
        catch
        {
            _recorder?.Release();
            _recorder?.Dispose();
            _recorder = null;
            if (_recordingPath is not null) File.Delete(_recordingPath);
            _recordingPath = null;
            throw;
        }
#else
        throw new InvalidOperationException("语音录入目前在安卓版提供。 ");
#endif
    }

    public async Task<LocalVoiceNote> StopAsync()
    {
#if ANDROID
        if (_recorder is null || _recordingPath is null) throw new InvalidOperationException("还没有开始录音。 ");
        var path = _recordingPath;
        try { _recorder.Stop(); }
        catch
        {
            File.Delete(path);
            throw new InvalidOperationException("录音太短或已中断，请重新录入。 ");
        }
        finally
        {
            _recorder.Release();
            _recorder.Dispose();
            _recorder = null;
            _recordingPath = null;
        }
        var note = new LocalVoiceNote { AudioPath = path };
        await store.SaveVoiceNoteAsync(note);
        return note;
#else
        throw new InvalidOperationException("语音录入目前在安卓版提供。 ");
#endif
    }

    public async Task<string> TranscribeAsync(LocalVoiceNote note)
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            throw new InvalidOperationException("录音已保存在手机，联网后点‘转为文字’。 ");
        var key = await SecureStorage.Default.GetAsync(KeyName);
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("请先到设置中配置语音转写服务的 API Key。 ");
        var file = new FileInfo(note.AudioPath);
        if (!file.Exists) throw new FileNotFoundException("找不到本地录音。 ", note.AudioPath);
        if (file.Length > 25 * 1024 * 1024) throw new InvalidOperationException("单段录音请控制在 25 MB 以内。 ");
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("whisper-1"), "model");
        form.Add(new StringContent("zh"), "language");
        await using var stream = File.OpenRead(note.AudioPath);
        var audio = new StreamContent(stream);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/mp4");
        form.Add(audio, "file", Path.GetFileName(note.AudioPath));
        using var response = await client.PostAsync(Endpoint, form);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"语音转写未完成（{(int)response.StatusCode}），录音仍在本机。 ");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var text = doc.RootElement.GetProperty("text").GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("没有识别出文字，录音仍在本机。 ");
        note.Transcript = text;
        note.Transcribed = true;
        await store.SaveVoiceNoteAsync(note);
        return text;
    }

    public async Task UploadPendingAsync()
    {
        foreach (var note in await store.GetVoiceNotesAsync())
        {
            if (note.Uploaded || note.TaskId is null || !File.Exists(note.AudioPath)) continue;
            await api.UploadVoiceAsync(note.Id, note.TaskId, note.AudioPath);
            note.Uploaded = true;
            await store.SaveVoiceNoteAsync(note);
        }
    }

    public async Task<string?> GetOrDownloadAsync(string taskId)
    {
        var local = (await store.GetVoiceNotesAsync()).FirstOrDefault(x => x.TaskId == taskId && File.Exists(x.AudioPath));
        if (local is not null) return local.AudioPath;
        var id = await api.FindVoiceIdAsync(taskId);
        if (id is null) return null;
        var folder = Path.Combine(FileSystem.AppDataDirectory, "voice");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{id:N}.m4a");
        await api.DownloadVoiceAsync(id.Value, path);
        await store.SaveVoiceNoteAsync(new LocalVoiceNote { Id = id.Value.ToString(), TaskId = taskId, AudioPath = path, Uploaded = true });
        return path;
    }
}
