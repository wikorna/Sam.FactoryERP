using System.Security.Cryptography;
using System.Text;
using Auth.Application.Features.Login;
using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Auth.Tests;

public sealed class LoginCommandHandlerTests : IDisposable
{
    // ─── Shared constants ──────────────────────────────────────────────
    private static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private const string TestUsername = "testuser";
    private const string TestPassword = "P@ssw0rd!";
    private const string TestIp = "127.0.0.1";
    private const string TestUserAgent = "xUnit/1.0";
    private const string TestAccessToken = "eyJ.access.token";
    private const string TestRawRefresh = "raw-refresh-base64url";
    private const string TestRefreshHash = "hashed-refresh";
    private static readonly DateTime TestExpires = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string[] TestRoles = ["Admin", "Operator"];

    private static readonly LoginCommand DefaultCommand =
        new(TestUsername, TestPassword, TestIp, TestUserAgent);

    // ─── SUT factory ───────────────────────────────────────────────────
    private readonly FakeUserManager _userManager;
    private readonly Mock<IAuthDbContext> _dbMock;
    private readonly Mock<IJwtTokenService> _jwtTokenServiceMock;
    private readonly Mock<IRefreshTokenService> _refreshTokenServiceMock;
    private readonly ILogger<LoginCommandHandler> _logger;
    private readonly LoginCommandHandler _sut;

    /// <summary>Captures the RefreshToken entity passed to DbSet.Add.</summary>
    private readonly List<RefreshToken> _addedRefreshTokens = [];

    public LoginCommandHandlerTests()
    {
        _userManager = new FakeUserManager();

        // --- IAuthDbContext ---
        _dbMock = new Mock<IAuthDbContext>();
        // Capture calls to RefreshTokens.Add via a stub DbSet
        var fakeDbSetMock = new Mock<DbSet<RefreshToken>>();
        fakeDbSetMock.Setup(x => x.Add(It.IsAny<RefreshToken>()))
            .Callback<RefreshToken>(ci => _addedRefreshTokens.Add(ci));

        _dbMock.Setup(x => x.RefreshTokens).Returns(fakeDbSetMock.Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // --- IJwtTokenService ---
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();
        _jwtTokenServiceMock
            .Setup(x => x.GenerateAccessTokenAsync(
                It.IsAny<Guid>(), It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestAccessToken, "jti-abc", TestExpires));

        // --- IRefreshTokenService ---
        _refreshTokenServiceMock = new Mock<IRefreshTokenService>();
        _refreshTokenServiceMock.Setup(x => x.GenerateRawToken()).Returns(TestRawRefresh);
        _refreshTokenServiceMock.Setup(x => x.HashToken(TestRawRefresh)).Returns(TestRefreshHash);

        // --- Logger (NullLogger — we don't assert on log content) ---
        _logger = new NullLogger<LoginCommandHandler>();

        _sut = new LoginCommandHandler(
            _userManager.Instance,
            _dbMock.Object,
            _jwtTokenServiceMock.Object,
            _refreshTokenServiceMock.Object,
            _logger);
    }

    public void Dispose() => _userManager.Dispose();

    // ═══════════════════════════════════════════════════════════════════
    // 1) User not found → generic fail + dummy hash verify invoked
    // ═══════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Handle_UserNotFound_ReturnsGenericFail_AndPerformsDummyHash()
    {
        // Arrange
        _userManager.SetFindByNameResult(null);

        // Act
        var result = await _sut.Handle(DefaultCommand, CancellationToken.None);

        // Assert — generic fail, no user enumeration
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Message.Should().Be("Invalid credentials.");
        result.Failure.RemainingAttempts.Should().BeNull();
        result.Failure.LockoutEndsAtUtc.Should().BeNull();

        // Verify the dummy hash path was executed (PasswordHasher was touched)
        _userManager.PasswordHasherCallCount.Should().BeGreaterThanOrEqualTo(1,
            "dummy hash verify should be invoked to mitigate timing attacks");
    }

    // ═══════════════════════════════════════════════════════════════════
    // 2) User inactive → fail
    // ═══════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Handle_UserInactive_ReturnsFail()
    {
        // Arrange
        var user = CreateUser(isActive: false);
        _userManager.SetFindByNameResult(user);

        // Act
        var result = await _sut.Handle(DefaultCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.Message.Should().Be("Invalid credentials.");
    }

    // ═══════════════════════════════════════════════════════════════════
    // 3) User locked out (pre-check) → fail with lockout metadata
    // ═══════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Handle_UserLockedOut_ReturnsFail_WithLockoutMetadata()
    {
        // Arrange
        var lockoutEnd = new DateTimeOffset(2026, 3, 1, 15, 0, 0, TimeSpan.Zero);
        var user = CreateUser();
        user.LockoutEnd = lockoutEnd;

        _userManager.SetFindByNameResult(user);
        _userManager.SetIsLockedOut(true);

        // Act
        var result = await _sut.Handle(DefaultCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.RemainingAttempts.Should().Be(0);
        result.Failure.LockoutEndsAtUtc.Should().Be(lockoutEnd.UtcDateTime);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 4) Wrong password, not locked yet → fail with RemainingAttempts
    // ═══════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Handle_WrongPassword_NotLocked_ReturnsFail_WithRemainingAttempts()
    {
        // Arrange
        var user = CreateUser();
        _userManager.SetFindByNameResult(user);
        _userManager.SetIsLockedOut(false);
        _userManager.SetCheckPasswordResult(false);
        _userManager.SetMaxFailedAccessAttempts(5);
        _userManager.SetAccessFailedCount(2);

        // Act
        var result = await _sut.Handle(DefaultCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.RemainingAttempts.Should().Be(3); // 5 - 2 = 3
        result.Failure.LockoutEndsAtUtc.Should().BeNull();

        _userManager.AccessFailedAsyncCalled.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // 5) Wrong password causing lockout → fail with RemainingAttempts=0
    // ═══════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Handle_WrongPassword_CausesLockout_ReturnsFail_WithZeroRemaining()
    {
        // Arrange
        var lockoutEnd = new DateTimeOffset(2026, 3, 1, 16, 0, 0, TimeSpan.Zero);
        var user = CreateUser();
        user.LockoutEnd = lockoutEnd;

        _userManager.SetFindByNameResult(user);
        _userManager.SetCheckPasswordResult(false);
        _userManager.SetMaxFailedAccessAttempts(5);
        // After AccessFailedAsync, the user becomes locked
        _userManager.SetIsLockedOutSequence(false, true);

        // Act
        var result = await _sut.Handle(DefaultCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.Failure!.RemainingAttempts.Should().Be(0);
        result.Failure.LockoutEndsAtUtc.Should().Be(lockoutEnd.UtcDateTime);
    }

    // ═══════════════════════════════════════════════════════════════════
    // 6) Success path → returns tokens + persists refresh token
    // ═══════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Handle_ValidCredentials_ReturnsTokens_AndPersistsRefreshToken()
    {
        // Arrange
        var user = CreateUser();
        _userManager.SetFindByNameResult(user);
        _userManager.SetIsLockedOut(false);
        _userManager.SetCheckPasswordResult(true);
        _userManager.SetRoles(TestRoles);
        _userManager.SetAccessFailedCount(1);

        // Act
        var beforeUtc = DateTime.UtcNow;
        var result = await _sut.Handle(DefaultCommand, CancellationToken.None);
        var afterUtc = DateTime.UtcNow;

        // Assert — result
        result.IsSuccess.Should().BeTrue(because: result.Failure?.Message ?? "Failure should be null");
        result.Tokens.Should().NotBeNull();
        result.Tokens!.AccessToken.Should().Be(TestAccessToken);
        result.Tokens.RefreshToken.Should().Be(TestRawRefresh);
        result.Tokens.ExpiresAtUtc.Should().Be(TestExpires);

        // Assert — UserManager interactions
        _userManager.ResetAccessFailedCountCalled.Should().BeTrue();
        _userManager.UpdateAsyncCalled.Should().BeTrue();
        user.LastLoginAtUtc.Should().NotBeNull()
            .And.BeOnOrAfter(beforeUtc)
            .And.BeOnOrBefore(afterUtc);

        // Assert — JWT service called with correct args
        _jwtTokenServiceMock.Verify(x => x.GenerateAccessTokenAsync(
            user.Id,
            TestUsername,
            It.Is<IEnumerable<string>>(r => r.SequenceEqual(TestRoles)),
            It.IsAny<CancellationToken>()), Times.Once);

        // Assert — Refresh token service
        _refreshTokenServiceMock.Verify(x => x.GenerateRawToken(), Times.Once);
        _refreshTokenServiceMock.Verify(x => x.HashToken(TestRawRefresh), Times.Once);

        // Assert — persisted RefreshToken entity
        _addedRefreshTokens.Should().ContainSingle();
        var stored = _addedRefreshTokens[0];
        stored.UserId.Should().Be(user.Id);
        stored.TokenHash.Should().Be(TestRefreshHash);
        stored.CreatedByIp.Should().Be(TestIp);
        stored.UserAgentHash.Should().Be(ComputeSha256Hex(TestUserAgent));
        stored.ExpiresAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));

        // Assert — SaveChangesAsync called
        _dbMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private static ApplicationUser CreateUser(bool isActive = true) => new()
    {
        Id = TestUserId,
        UserName = TestUsername,
        Email = $"{TestUsername}@example.com",
        IsActive = isActive,
        LockoutEnabled = true,
        PasswordHash = "dummy-hash",
    };

    private static string ComputeSha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    // ═══════════════════════════════════════════════════════════════════
    // FakeUserManager — lightweight wrapper around a real UserManager
    // with an in-memory IUserStore mock so Identity plumbing works.
    // Only the virtual methods used by LoginCommandHandler are overridden.
    // ═══════════════════════════════════════════════════════════════════
    private sealed class FakeUserManager : IDisposable
    {
        private readonly UserManager<ApplicationUser> _inner;

        private ApplicationUser? _findResult;
        private bool _checkPasswordResult;
        private readonly Queue<bool> _lockedOutQueue = new();
        private int _accessFailedCount;
        private string[] _roles = [];

        /// <summary>Sentinel date far in the future used when lockout is active.</summary>
        private static readonly DateTimeOffset FutureLockout = DateTimeOffset.UtcNow.AddYears(1);

        public int PasswordHasherCallCount { get; private set; }
        public bool AccessFailedAsyncCalled { get; private set; }
        public bool ResetAccessFailedCountCalled { get; private set; }
        public bool UpdateAsyncCalled { get; private set; }

        public UserManager<ApplicationUser> Instance => _inner;

        public FakeUserManager()
        {
            // Combined store implementing password, lockout, and role interfaces
            var storeMock = new Mock<IUserStore<ApplicationUser>>();
            var passwordStoreMock = storeMock.As<IUserPasswordStore<ApplicationUser>>();
            var lockoutStoreMock = storeMock.As<IUserLockoutStore<ApplicationUser>>();
            var roleStoreMock = storeMock.As<IUserRoleStore<ApplicationUser>>();

            // Password store
            passwordStoreMock.Setup(x => x.FindByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _findResult);
            passwordStoreMock.Setup(x => x.GetPasswordHashAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ApplicationUser user, CancellationToken _) => user.PasswordHash!);
            storeMock.Setup(x => x.GetUserIdAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ApplicationUser user, CancellationToken _) => user.Id.ToString());
            storeMock.Setup(x => x.GetUserNameAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ApplicationUser user, CancellationToken _) => user.UserName);
            storeMock.Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
                .Callback(() => UpdateAsyncCalled = true)
                .ReturnsAsync(IdentityResult.Success);

            // Lockout store — use the queue to drive IsLockedOutAsync
            lockoutStoreMock.Setup(x => x.GetLockoutEnabledAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            lockoutStoreMock.Setup(x => x.GetLockoutEndDateAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    // Dequeue next value; if queue empty, return null (not locked)
                    if (_lockedOutQueue.Count > 0)
                    {
                        bool locked = _lockedOutQueue.Dequeue();
                        // Peek-requeue: if this was the last item, keep it for repeated calls
                        if (_lockedOutQueue.Count == 0) _lockedOutQueue.Enqueue(locked);
                        if (locked)
                        {
                            // Always return a future date so UserManager.IsLockedOutAsync
                            // considers the user locked (it compares lockoutEnd > UtcNow).
                            // The handler reads user.LockoutEnd separately for the response.
                            return FutureLockout;
                        }
                    }
                    return null;
                });
            lockoutStoreMock.Setup(x => x.GetAccessFailedCountAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _accessFailedCount);
            lockoutStoreMock.Setup(x => x.IncrementAccessFailedCountAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
                .Callback(() => AccessFailedAsyncCalled = true)
                .ReturnsAsync(() => _accessFailedCount);
            lockoutStoreMock.Setup(x => x.ResetAccessFailedCountAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
                .Callback(() => ResetAccessFailedCountCalled = true)
                .Returns(Task.CompletedTask);
            lockoutStoreMock.Setup(x => x.SetLockoutEndDateAsync(It.IsAny<ApplicationUser>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Role store
            roleStoreMock.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _roles);

            // Password hasher that tracks calls
            var hasherMock = new Mock<IPasswordHasher<ApplicationUser>>();
            hasherMock.Setup(x => x.HashPassword(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .Callback(() => PasswordHasherCallCount++)
                .Returns("dummy-hash");
            hasherMock.Setup(x => x.VerifyHashedPassword(
                    It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback(() => PasswordHasherCallCount++)
                .Returns(() => _checkPasswordResult
                    ? PasswordVerificationResult.Success
                    : PasswordVerificationResult.Failed);

            // Identity options
            var identityOptions = new IdentityOptions
            {
                Lockout =
                {
                    MaxFailedAccessAttempts = 5,
                    DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15),
                    AllowedForNewUsers = true,
                }
            };
            var optionsAccessorMock = new Mock<IOptions<IdentityOptions>>();
            optionsAccessorMock.Setup(x => x.Value).Returns(identityOptions);

            _inner = new UserManager<ApplicationUser>(
                storeMock.Object,
                optionsAccessorMock.Object,
                hasherMock.Object,
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null!, // services
                new NullLogger<UserManager<ApplicationUser>>());
        }

        public void Dispose() => _inner.Dispose();

        // ── Configuration methods ──

        public void SetFindByNameResult(ApplicationUser? user)
            => _findResult = user;

        public void SetCheckPasswordResult(bool valid)
            => _checkPasswordResult = valid;

        /// <summary>Sets a single locked-out value used for all IsLockedOutAsync calls.</summary>
        public void SetIsLockedOut(bool locked)
        {
            _lockedOutQueue.Clear();
            _lockedOutQueue.Enqueue(locked);
        }

        /// <summary>
        /// Sets a sequence: first IsLockedOutAsync call returns values[0], second returns values[1], etc.
        /// The last value is reused for all subsequent calls.
        /// Used when lockout state changes between the pre-check and post-AccessFailed check.
        /// </summary>
        public void SetIsLockedOutSequence(params bool[] values)
        {
            _lockedOutQueue.Clear();
            foreach (var v in values)
                _lockedOutQueue.Enqueue(v);
        }

        public void SetMaxFailedAccessAttempts(int max)
            => _inner.Options.Lockout.MaxFailedAccessAttempts = max;

        public void SetAccessFailedCount(int count)
            => _accessFailedCount = count;

        public void SetRoles(params string[] roles)
            => _roles = roles;
    }
}
