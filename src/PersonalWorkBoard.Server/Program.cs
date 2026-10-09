using System.Text.Json.Serialization;
using Dapper;
using PersonalWorkBoard.Contracts;
using PersonalWorkBoard.Server.Database;
using PersonalWorkBoard.Server.Options;
using PersonalWorkBoard.Server.Security;
using PersonalWorkBoard.Server.Sync;

DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection("Server"));
builder.Services.Configure<BootstrapOptions>(builder.Configuration.GetSection("Bootstrap"));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<PairingService>();
builder.Services.AddSingleton<SyncService>();
builder.Services.AddSingleton<VoiceFileService>();
builder.WebHost.UseUrls("http://0.0.0.0:5088");

var app = builder.Build();
await app.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync(app.Lifetime.ApplicationStopping);
await app.Services.GetRequiredService<AuthService>().EnsureAdminAsync(app.Lifetime.ApplicationStopping);

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.CacheControl = "no-store";
    await next();
});

app.MapGet("/api/health", (Microsoft.Extensions.Options.IOptions<ServerOptions> options) =>
    Results.Ok(new HealthResponse("ok", options.Value.Name, DateTimeOffset.UtcNow, "v1")));

app.MapPost("/api/auth/login", async (LoginRequest request, AuthService auth, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        return Results.BadRequest(new { error = "用户名和密码不能为空" });
    var result = await auth.LoginAsync(request, ct);
    return result is null ? Results.Unauthorized() : Results.Ok(result);
});

app.MapPost("/api/pairing/tickets", async (HttpContext context, AuthService auth, PairingService pairing, CancellationToken ct) =>
{
    var principal = await auth.AuthenticateAsync(context.Request, ct);
    if (principal is null) return Results.Unauthorized();
    return Results.Ok(await pairing.CreateAsync(principal, ct));
});

app.MapPost("/api/pairing/redeem", async (RedeemPairingTicketRequest request, PairingService pairing, CancellationToken ct) =>
{
    var result = await pairing.RedeemAsync(request, ct);
    return result is null ? Results.BadRequest(new { error = "二维码已失效或已使用，请在PC端重新生成" }) : Results.Ok(result);
});

app.MapGet("/api/sync/pull", async (HttpContext context, long? since, int? limit, AuthService auth, SyncService sync, CancellationToken ct) =>
{
    var principal = await auth.AuthenticateAsync(context.Request, ct);
    if (principal is null) return Results.Unauthorized();
    return Results.Ok(await sync.PullAsync(principal.UserId, since ?? 0, limit ?? 500, ct));
});

app.MapPost("/api/sync/push", async (HttpContext context, PushChangesRequest request, AuthService auth, SyncService sync, CancellationToken ct) =>
{
    var principal = await auth.AuthenticateAsync(context.Request, ct);
    if (principal is null) return Results.Unauthorized();
    if (request.Mutations.Count > 200) return Results.BadRequest(new { error = "一次最多同步200条变更" });
    try
    {
        return Results.Ok(await sync.PushAsync(principal, request, ct));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPut("/api/voice/{id:guid}", async (Guid id, Guid taskId, HttpContext context, AuthService auth, VoiceFileService voice, CancellationToken ct) =>
{
    var principal = await auth.AuthenticateAsync(context.Request, ct);
    if (principal is null) return Results.Unauthorized();
    if (context.Request.ContentLength is null or <= 0 or > 25 * 1024 * 1024 || context.Request.ContentType != "audio/mp4")
        return Results.BadRequest(new { error = "仅接受 25 MB 以内的 M4A 录音" });
    return await voice.SaveAsync(principal.UserId, taskId, id, context.Request.Body, ct)
        ? Results.Ok() : Results.NotFound();
});

app.MapGet("/api/voice/by-task/{taskId:guid}", async (Guid taskId, HttpContext context, AuthService auth, VoiceFileService voice, CancellationToken ct) =>
{
    var principal = await auth.AuthenticateAsync(context.Request, ct);
    if (principal is null) return Results.Unauthorized();
    return Results.Ok(await voice.FindByTaskAsync(principal.UserId, taskId, ct));
});

app.MapGet("/api/voice/{id:guid}", async (Guid id, HttpContext context, AuthService auth, VoiceFileService voice, CancellationToken ct) =>
{
    var principal = await auth.AuthenticateAsync(context.Request, ct);
    if (principal is null) return Results.Unauthorized();
    var path = await voice.GetPathAsync(principal.UserId, id, ct);
    return path is null ? Results.NotFound() : Results.File(path, "audio/mp4", enableRangeProcessing: true);
});

app.Run();

public partial class Program;
