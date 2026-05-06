namespace FriendMap.Api.Contracts;

public record DevLoginRequest(
    string Nickname,
    string? DisplayName);

public record AppleLoginRequest(
    string IdentityToken,
    string? AuthorizationCode,
    string? FullName);

public record AuthUserDto(
    Guid UserId,
    string Nickname,
    string? DisplayName);

public record AuthTokenResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    AuthUserDto User);
