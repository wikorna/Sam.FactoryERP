namespace Auth.Application.Features.CurrentUser;

/// <summary>
/// Profile response returned by the <c>/api/auth/me</c> endpoint.
/// Contains the user's identity, roles, and accessible ERP modules.
/// </summary>
public sealed record MeResponse
{
    public required Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
    public required IReadOnlyList<AppDto> Apps { get; init; }
}

/// <summary>
/// Lightweight DTO representing an ERP module/app the user can access.
/// </summary>
public sealed record AppDto
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string Route { get; init; }
}

