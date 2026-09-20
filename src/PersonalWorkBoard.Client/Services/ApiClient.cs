using System.Net.Http.Headers;
using System.Net.Http.Json;
using PersonalWorkBoard.Contracts;

namespace PersonalWorkBoard.Client.Services;

public sealed class ApiClient
{
    private const string ServerKey = "server_url";
    private const string TokenKey = "access_token";
    private const string DeviceKey = "device_id";

    public async Task<bool> HasSessionAsync() =>
        !string.IsNullOrWhiteSpace(await SecureStorage.Default.GetAsync(ServerKey)) &&
        !string.IsNullOrWhiteSpace(await SecureStorage.Default.GetAsync(TokenKey));

    public async Task LoginAsync(string serverUrl, string userName, string password)
    {
        var client = CreateClient(serverUrl, null);
        var response = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(userName, password, DeviceInfo.Name, DeviceInfo.Platform.ToString()));
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("登录失败，请检查服务器地址、用户名和密码。 ");
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>() ?? throw new InvalidOperationException("服务器返回无效。 ");
        await SaveSessionAsync(serverUrl, result.AccessToken, result.DeviceId);
    }

    public async Task<PairingTicketResponse> CreatePairingTicketAsync()
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("api/pairing/tickets", new CreatePairingTicketRequest(DeviceInfo.Name));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PairingTicketResponse>() ?? throw new InvalidOperationException("配对票据无效。 ");
    }

    public async Task RedeemPairingAsync(string qrPayload)
    {
        var uri = new Uri(qrPayload);
        if (!string.Equals(uri.Scheme, "pwb", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("这不是个人工作看板二维码。 ");
        var query = ParseQuery(uri.Query);
        if (!query.TryGetValue("server", out var serverUrl) || !query.TryGetValue("ticket", out var ticket))
            throw new InvalidOperationException("二维码缺少配对信息。 ");
        var client = CreateClient(serverUrl, null);
        var response = await client.PostAsJsonAsync("api/pairing/redeem", new RedeemPairingTicketRequest(ticket, DeviceInfo.Name, DeviceInfo.Platform.ToString()));
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("二维码已过期，请在PC端重新生成。 ");
        var result = await response.Content.ReadFromJsonAsync<RedeemPairingTicketResponse>() ?? throw new InvalidOperationException("配对响应无效。 ");
        await SaveSessionAsync(serverUrl, result.AccessToken, result.DeviceId);
    }

    public async Task<PullChangesResponse> PullAsync(long since)
    {
        var client = await CreateAuthenticatedClientAsync();
        return await client.GetFromJsonAsync<PullChangesResponse>($"api/sync/pull?since={since}&limit=500")
            ?? new PullChangesResponse(since, []);
    }

    public async Task<PushChangesResponse> PushAsync(PushChangesRequest request)
    {
        var client = await CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("api/sync/push", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PushChangesResponse>()
            ?? throw new InvalidOperationException("同步响应无效。 ");
    }

    public async Task<Guid> GetDeviceIdAsync()
    {
        var value = await SecureStorage.Default.GetAsync(DeviceKey);
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    public async Task<string> GetServerUrlAsync() => await SecureStorage.Default.GetAsync(ServerKey) ?? string.Empty;

    public Task LogoutAsync()
    {
        SecureStorage.Default.Remove(TokenKey);
        SecureStorage.Default.Remove(DeviceKey);
        return Task.CompletedTask;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var server = await SecureStorage.Default.GetAsync(ServerKey) ?? throw new InvalidOperationException("尚未配置服务器。 ");
        var token = await SecureStorage.Default.GetAsync(TokenKey) ?? throw new InvalidOperationException("登录已失效。 ");
        return CreateClient(server, token);
    }

    private static HttpClient CreateClient(string serverUrl, string? token)
    {
        var normalized = serverUrl.Trim().TrimEnd('/') + "/";
        var client = new HttpClient { BaseAddress = new Uri(normalized), Timeout = TimeSpan.FromSeconds(20) };
        if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task SaveSessionAsync(string serverUrl, string token, Guid deviceId)
    {
        await SecureStorage.Default.SetAsync(ServerKey, serverUrl.Trim().TrimEnd('/'));
        await SecureStorage.Default.SetAsync(TokenKey, token);
        await SecureStorage.Default.SetAsync(DeviceKey, deviceId.ToString());
    }

    private static Dictionary<string, string> ParseQuery(string query) => query.TrimStart('?')
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(item => item.Split('=', 2))
        .Where(parts => parts.Length == 2)
        .ToDictionary(parts => Uri.UnescapeDataString(parts[0]), parts => Uri.UnescapeDataString(parts[1]), StringComparer.OrdinalIgnoreCase);
}
