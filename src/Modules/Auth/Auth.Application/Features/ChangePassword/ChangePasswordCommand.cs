using MediatR;

namespace Auth.Application.Features.ChangePassword;

/// <summary>Changes the authenticated user's password.</summary>
public sealed record ChangePasswordCommand(
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : IRequest<ChangePasswordResult>;

/// <summary>Result of a password change attempt.</summary>
public sealed record ChangePasswordResult(bool IsSuccess, string? ErrorMessage = null);
