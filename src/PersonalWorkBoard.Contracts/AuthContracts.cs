namespace PersonalWorkBoard.Contracts;

public sealed record LoginRequest(string UserName, string Password, string DeviceName, string DevicePlatform);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, Guid UserId, Guid DeviceId, string DisplayName);

public sealed record CreatePairingTicketRequest(string PcName);
public sealed record PairingTicketResponse(string Ticket, string QrPayload, DateTimeOffset ExpiresAt);
public sealed record RedeemPairingTicketRequest(string Ticket, string DeviceName, string DevicePlatform);
public sealed record RedeemPairingTicketResponse(string AccessToken, DateTimeOffset ExpiresAt, Guid UserId, Guid DeviceId, string ServerName);
