using Auth.Application.Features.Login;
using MediatR;

namespace Auth.Application.Features.RefreshToken;

/// <summary>Rotates a refresh token and issues a new access + refresh token pair.</summary>
public sealed record RefreshTokenCommand(
    string RefreshToken,
    string IpAddress,
    string UserAgent) : IRequest<AuthTokenResponse>;
