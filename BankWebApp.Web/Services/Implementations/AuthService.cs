using BankWebApp.Web.Data;
using BankWebApp.Web.DTOs.Auth;
using BankWebApp.Web.DTOs.Security;
using BankWebApp.Web.Helpers;
using BankWebApp.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

public class AuthService : IAuthService
{
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private readonly BankDbContext _dbContext;
    private readonly ISecurityEventService _securityEventService;
    private readonly IDatabaseClockService _databaseClockService;

    public AuthService(
        BankDbContext dbContext,
        ISecurityEventService securityEventService,
        IDatabaseClockService databaseClockService)
    {
        _dbContext = dbContext;
        _securityEventService = securityEventService;
        _databaseClockService = databaseClockService;
    }

    public async Task<LoginResultDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var usernameOrEmail = request.UsernameOrEmail.Trim();

        if (string.IsNullOrWhiteSpace(usernameOrEmail))
        {
            return Failed("Хэрэглэгчийн нэр эсвэл имэйл хоосон байна.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Failed("Нууц үг хоосон байна.");
        }

        var clock = await _databaseClockService.GetSnapshotAsync(cancellationToken);
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == usernameOrEmail || u.Email == usernameOrEmail, cancellationToken);

        if (user is null)
        {
            await _securityEventService.LogAsync(
                null,
                usernameOrEmail,
                "LOGIN_FAILED_UNKNOWN_USER",
                false,
                "Login failed because the username or email was not found.",
                cancellationToken);

            return Failed("Хэрэглэгчийн нэр эсвэл нууц үг буруу байна.");
        }

        if (!user.IsActive)
        {
            await _securityEventService.LogAsync(
                user.Id,
                usernameOrEmail,
                "LOGIN_FAILED_INACTIVE_USER",
                false,
                "Login blocked because the user is inactive.",
                cancellationToken);

            return Failed("Хэрэглэгч идэвхгүй байна.");
        }

        if (IsLocked(user, clock))
        {
            var lockedUntilLocal = ToLocalDisplayTime(user.LockedUntilUtc, user.LockedUntil);

            await _securityEventService.LogAsync(
                user.Id,
                usernameOrEmail,
                "LOGIN_BLOCKED_LOCKED_ACCOUNT",
                false,
                $"Login blocked until UTC {user.LockedUntilUtc:yyyy-MM-dd HH:mm:ss}.",
                cancellationToken);

            return Failed($"Таны эрх түр түгжигдсэн байна. {lockedUntilLocal:yyyy-MM-dd HH:mm} цагаас хойш дахин оролдоно уу.");
        }

        if (HasExpiredLock(user, clock))
        {
            ClearLock(user);
        }

        if (!VerifyPassword(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount += 1;
            user.LastFailedLoginAtUtc = clock.UtcNow;
            user.LastFailedLoginAt = MongoliaClock.ToMongoliaTime(clock.UtcNow);
            user.LastFailedLoginServerTick = clock.ServerTickMilliseconds;

            var remainingAttempts = MaxFailedLoginAttempts - user.FailedLoginCount;
            if (user.FailedLoginCount >= MaxFailedLoginAttempts)
            {
                var lockedUntilUtc = clock.UtcNow.Add(LockDuration);
                user.LockedUntilUtc = lockedUntilUtc;
                user.LockedUntil = MongoliaClock.ToMongoliaTime(lockedUntilUtc);
                user.LockedUntilServerTick = clock.ServerTickMilliseconds + (long)LockDuration.TotalMilliseconds;

                await _securityEventService.LogAsync(
                    user.Id,
                    usernameOrEmail,
                    "ACCOUNT_LOCKED_FAILED_LOGIN_LIMIT",
                    false,
                    $"Account locked for {LockDuration.TotalMinutes:0} minutes after {user.FailedLoginCount} failed login attempts.",
                    cancellationToken);

                return Failed($"Нууц үг 5 удаа буруу орсон тул эрх 15 минут түгжигдлээ. {user.LockedUntil:yyyy-MM-dd HH:mm} цагаас хойш дахин оролдоно уу.");
            }

            await _securityEventService.LogAsync(
                user.Id,
                usernameOrEmail,
                "LOGIN_FAILED_BAD_PASSWORD",
                false,
                $"Bad password. Remaining attempts before temporary lock: {remainingAttempts}.",
                cancellationToken);

            return Failed($"Хэрэглэгчийн нэр эсвэл нууц үг буруу байна. Түр түгжигдэхээс өмнө {remainingAttempts} оролдлого үлдлээ.");
        }

        user.FailedLoginCount = 0;
        user.LastLoginAtUtc = clock.UtcNow;
        user.LastLoginAt = MongoliaClock.ToMongoliaTime(clock.UtcNow);
        user.LastFailedLoginAtUtc = null;
        user.LastFailedLoginAt = null;
        user.LastFailedLoginServerTick = null;
        ClearLock(user);

        await _securityEventService.LogAsync(
            user.Id,
            usernameOrEmail,
            "LOGIN_SUCCESS",
            true,
            "Login succeeded.",
            cancellationToken);

        return new LoginResultDto
        {
            Success = true,
            UserId = user.Id,
            Username = user.Username,
            FullName = BuildFullName(user.FirstName, user.LastName),
            Role = user.Role
        };
    }

    private static bool IsLocked(Data.Entities.User user, DatabaseTimeSnapshotDto clock)
    {
        if (clock.ServerTickMilliseconds is not null && user.LockedUntilServerTick is not null)
        {
            return user.LockedUntilServerTick > clock.ServerTickMilliseconds;
        }

        return user.LockedUntilUtc is not null && user.LockedUntilUtc > clock.UtcNow;
    }

    private static bool HasExpiredLock(Data.Entities.User user, DatabaseTimeSnapshotDto clock)
    {
        if (clock.ServerTickMilliseconds is not null && user.LockedUntilServerTick is not null)
        {
            return user.LockedUntilServerTick <= clock.ServerTickMilliseconds;
        }

        return user.LockedUntilUtc is not null && user.LockedUntilUtc <= clock.UtcNow;
    }

    private static void ClearLock(Data.Entities.User user)
    {
        user.LockedUntil = null;
        user.LockedUntilUtc = null;
        user.LockedUntilServerTick = null;
        user.FailedLoginCount = 0;
    }

    private static DateTime ToLocalDisplayTime(DateTime? utcDateTime, DateTime? fallbackLocal)
    {
        return utcDateTime is null
            ? fallbackLocal ?? MongoliaClock.Now
            : MongoliaClock.ToMongoliaTime(utcDateTime.Value);
    }

    private static string? BuildFullName(string? firstName, string? lastName)
    {
        var fullName = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? null : fullName;
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        return passwordHash.StartsWith("$2", StringComparison.Ordinal)
            && BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }

    private static LoginResultDto Failed(string message)
    {
        return new LoginResultDto
        {
            Success = false,
            ErrorMessage = message
        };
    }
}
