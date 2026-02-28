using MediatR;

namespace Auth.Application.Features.CurrentUser;

/// <summary>
/// Retrieves the authenticated user's profile including roles and accessible apps.
/// </summary>
public sealed record GetMeQuery(Guid UserId) : IRequest<MeResponse>;

