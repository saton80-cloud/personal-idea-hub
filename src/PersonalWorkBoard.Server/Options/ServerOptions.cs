namespace PersonalWorkBoard.Server.Options;

public sealed class ServerOptions
{
    public string Name { get; set; } = "个人工作看板局域网服务器";
    public string PublicBaseUrl { get; set; } = "http://127.0.0.1:5088";
    public int SessionDays { get; set; } = 90;
    public int PairingMinutes { get; set; } = 5;
}

public sealed class BootstrapOptions
{
    public string AdminUserName { get; set; } = "admin";
    public string AdminDisplayName { get; set; } = "我的工作看板";
    public string AdminPassword { get; set; } = string.Empty;
}
