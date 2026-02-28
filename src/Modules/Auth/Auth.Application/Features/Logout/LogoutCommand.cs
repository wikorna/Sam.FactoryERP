using MediatR;

namespace Auth.Application.Features.Logout;

/// <summary>Revokes a refresh token session and optionally blacklists the access token JTI.</summary>
public sealed record LogoutCommand(
    string? RefreshToken,
    string? AccessToken) : IRequest;
