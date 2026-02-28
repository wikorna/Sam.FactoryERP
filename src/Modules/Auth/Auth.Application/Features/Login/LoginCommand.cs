using MediatR;

namespace Auth.Application.Features.Login;

/// <summary>Authenticates a user and returns access + refresh tokens.</summary>
public sealed record LoginCommand(
    string Username,
    string Password,
    string IpAddress,
    string UserAgent) : IRequest<LoginResult>;

/// <summary>
/// Either-pattern result: success (tokens) or failure (lockout metadata).
/// Prevents throwing exceptions for expected auth failures.
/// </summary>
public sealed record LoginResult
{
    public bool IsSuccess { get; init; }
    public AuthTokenResponse? Tokens { get; init; }
    public LoginFailedResponse? Failure { get; init; }

    public static LoginResult Ok(AuthTokenResponse tokens) =>
        new() { IsSuccess = true, Tokens = tokens };

    public static LoginResult Fail(LoginFailedResponse failure) =>
        new() { IsSuccess = false, Failure = failure };
}
