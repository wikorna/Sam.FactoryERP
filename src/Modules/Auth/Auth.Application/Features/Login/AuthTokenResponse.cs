namespace Auth.Application.Features.Login;

/// <summary>Stable public API response for login and refresh operations.</summary>
public sealed record AuthTokenResponse
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
}

/// <summary>
/// Returned on failed login. Returns generic message + optional lockout metadata.
/// Does NOT reveal whether the username exists (prevents user enumeration).
/// </summary>
public sealed record LoginFailedResponse
{
    /// <summary>Always "Invalid credentials." — never reveals which field failed.</summary>
    public string Message { get; init; } = "Invalid credentials.";

    /// <summary>Remaining attempts before lockout. Null if not applicable.</summary>
    public int? RemainingAttempts { get; init; }

    /// <summary>UTC timestamp when lockout ends. Null if not locked.</summary>
    public DateTime? LockoutEndsAtUtc { get; init; }
}
