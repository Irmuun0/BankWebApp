using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.Helpers;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

// AdminService-ийн Users үүрэгтэй хэсэг; тусдаа instance үүсгэхгүй.
public partial class AdminService
{
    public async Task<AdminPagedResultDto<AdminUserDto>> GetUsersAsync(
        AdminUserFilterDto? filter = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Users.AsNoTracking();
        var normalizedSearch = NormalizeSearch(filter?.Search);
        if (normalizedSearch is not null)
        {
            query = query.Where(user =>
                user.Username.Contains(normalizedSearch) ||
                user.Email.Contains(normalizedSearch) ||
                user.PhoneNumber.Contains(normalizedSearch) ||
                user.Role.Contains(normalizedSearch) ||
                ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Contains(normalizedSearch) ||
                ((user.LastName ?? "") + "-ийн " + (user.FirstName ?? "")).Contains(normalizedSearch));
        }

        var username = NormalizeSearch(filter?.Username);
        if (username is not null)
        {
            query = query.Where(user => user.Username.Contains(username));
        }

        var email = NormalizeSearch(filter?.Email);
        if (email is not null)
        {
            query = query.Where(user => user.Email.Contains(email));
        }

        var name = NormalizeSearch(filter?.Name);
        if (name is not null)
        {
            query = query.Where(user =>
                ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Contains(name) ||
                ((user.LastName ?? "") + " " + (user.FirstName ?? "")).Contains(name) ||
                ((user.LastName ?? "") + "-ийн " + (user.FirstName ?? "")).Contains(name));
        }

        var role = NormalizeSearch(filter?.Role)?.ToUpperInvariant();
        if (role is not null && role is not "ALL")
        {
            query = query.Where(user => user.Role == role);
        }

        var status = NormalizeSearch(filter?.Status)?.ToUpperInvariant();
        if (status is "ACTIVE")
        {
            query = query.Where(user => user.IsActive);
        }
        else if (status is "INACTIVE")
        {
            query = query.Where(user => !user.IsActive);
        }

        if (filter?.CreatedFrom is not null)
        {
            var start = filter.CreatedFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(user => user.CreatedAt >= start);
        }

        if (filter?.CreatedTo is not null)
        {
            var endExclusive = filter.CreatedTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(user => user.CreatedAt < endExclusive);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var pageInfo = NormalizePage(page, pageSize, totalItems);
        var users = await query
            .OrderByDescending(user => user.CreatedAt)
            .ThenByDescending(user => user.Id)
            .Skip((pageInfo.Page - 1) * pageInfo.PageSize)
            .Take(pageInfo.PageSize)
            .Select(user => new AdminUserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FullName = user.LastName == null || user.LastName == ""
                    ? (user.FirstName ?? "")
                    : user.FirstName == null || user.FirstName == ""
                        ? user.LastName + "-ийн"
                        : user.LastName + "-ийн " + user.FirstName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var user in users.Where(user => string.IsNullOrWhiteSpace(user.FullName)))
        {
            user.FullName = null;
        }

        return new AdminPagedResultDto<AdminUserDto>
        {
            Items = users,
            Page = pageInfo.Page,
            PageSize = pageInfo.PageSize,
            TotalItems = totalItems
        };
    }

    public async Task<AdminUserProfileDto?> GetUserProfileAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new AdminUserProfileDto
            {
                Id = item.Id,
                Username = item.Username,
                Email = item.Email,
                FirstName = item.FirstName,
                LastName = item.LastName,
                PhoneNumber = item.PhoneNumber,
                EmergencyPhoneNumber = item.EmergencyPhoneNumber,
                Role = item.Role,
                IsActive = item.IsActive,
                PasswordResetRequired = item.PasswordResetRequired,
                FailedLoginCount = item.FailedLoginCount,
                LockedUntil = item.LockedUntil,
                LastLoginAt = item.LastLoginAt,
                PasswordChangedAt = item.PasswordChangedAt,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        user.Accounts = await _dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.UserId == userId)
            .OrderByDescending(account => account.IsPrimary)
            .ThenByDescending(account => account.CreatedAt)
            .Select(account => new AdminUserAccountSummaryDto
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                AccountType = account.AccountType,
                Currency = account.Currency,
                Balance = account.Balance,
                IsActive = account.IsActive,
                IsPrimary = account.IsPrimary,
                OpenedAt = account.CreatedAt
            })
            .ToListAsync(cancellationToken);

        user.RecentAuditLogs = await _dbContext.AuditLogs
            .AsNoTracking()
            .Include(log => log.User)
            .Where(log => log.TargetType == "users" && log.TargetId == userId)
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Take(8)
            .Select(log => new AdminUserProfileAuditDto
            {
                Id = log.Id,
                Action = log.Action,
                Detail = log.Detail ?? string.Empty,
                NewValue = log.NewValue,
                ActorUsername = log.User == null ? null : log.User.Username,
                CreatedAt = log.CreatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var log in user.RecentAuditLogs)
        {
            log.ActionLabel = GetAuditActionLabel(log.Action);
            log.Detail = BuildAuditDisplayDetail(log.Action, log.Detail, log.NewValue);
        }

        return user;
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateUserProfileAsync(
        long adminUserId,
        UpdateAdminUserProfileDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == dto.UserId, cancellationToken);
        if (user is null)
        {
            return (false, "Хэрэглэгч олдсонгүй.");
        }

        var username = dto.Username.Trim();
        var email = dto.Email.Trim();
        var firstName = NormalizeOptional(dto.FirstName);
        var lastName = NormalizeOptional(dto.LastName);
        var phoneNumber = dto.PhoneNumber.Trim();
        var emergencyPhone = NormalizeOptional(dto.EmergencyPhoneNumber);

        if (username.Length < 3)
        {
            return (false, "Нэвтрэх нэр хамгийн багадаа 3 тэмдэгттэй байх ёстой.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            return (false, "И-мэйл хаяг буруу байна.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return (false, "Гар утасны дугаар хоосон байж болохгүй.");
        }

        var usernameExists = await _dbContext.Users
            .AnyAsync(item => item.Id != user.Id && item.Username == username, cancellationToken);
        if (usernameExists)
        {
            return (false, "Энэ нэвтрэх нэр бүртгэлтэй байна.");
        }

        var emailExists = await _dbContext.Users
            .AnyAsync(item => item.Id != user.Id && item.Email == email, cancellationToken);
        if (emailExists)
        {
            return (false, "Энэ и-мэйл хаяг бүртгэлтэй байна.");
        }

        var oldValue = new
        {
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.EmergencyPhoneNumber
        };

        user.Username = username;
        user.Email = email;
        user.FirstName = firstName;
        user.LastName = lastName;
        user.PhoneNumber = phoneNumber;
        user.EmergencyPhoneNumber = emergencyPhone;
        user.UpdatedAt = MongoliaClock.Now;

        AddAuditLog(
            adminUserId,
            "ADMIN_USER_PROFILE_UPDATED",
            "users",
            user.Id,
            oldValue,
            new
            {
                user.Username,
                user.Email,
                user.FirstName,
                user.LastName,
                user.PhoneNumber,
                user.EmergencyPhoneNumber
            },
            $"Admin updated user {user.Username} profile settings.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Хэрэглэгчийн мэдээлэл амжилттай шинэчлэгдлээ.");
    }

    public async Task<(bool Success, string? ErrorMessage)> SetUserPasswordResetRequiredAsync(
        long adminUserId,
        long userId,
        bool isRequired,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "Хэрэглэгч олдсонгүй.");
        }

        if (user.PasswordResetRequired == isRequired)
        {
            return (true, isRequired
                ? "Нууц үг шинэчлэх шаардлага аль хэдийн тавигдсан байна."
                : "Нууц үг шинэчлэх шаардлага аль хэдийн цуцлагдсан байна.");
        }

        var oldValue = new { user.PasswordResetRequired };
        user.PasswordResetRequired = isRequired;
        user.UpdatedAt = MongoliaClock.Now;

        AddAuditLog(
            adminUserId,
            "ADMIN_USER_PASSWORD_RESET_REQUIRED_UPDATED",
            "users",
            user.Id,
            oldValue,
            new { user.PasswordResetRequired },
            isRequired
                ? $"Admin required user {user.Username} to reset password."
                : $"Admin cancelled password reset requirement for user {user.Username}.");

        _dbContext.Notifications.Add(new Notification
        {
            UserId = user.Id,
            NotificationType = "SECURITY_REVIEW_UPDATE",
            Title = "Нууц үг шинэчлэх шаардлага",
            Message = isRequired
                ? "Phoebe Bank таны бүртгэлд нууц үг шинэчлэх шаардлага тавилаа. Дараагийн нэвтрэлтээр шинэ нууц үгээ тохируулна уу."
                : "Phoebe Bank таны бүртгэл дээрх нууц үг шинэчлэх шаардлагыг цуцаллаа.",
            IsRead = false,
            CreatedAt = user.UpdatedAt
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, isRequired
            ? "Хэрэглэгч дараагийн нэвтрэлтээр нууц үгээ шинэчлэх шаардлагатай боллоо."
            : "Нууц үг шинэчлэх шаардлага цуцлагдлаа.");
    }

    public async Task<(bool Success, string? ErrorMessage)> UnlockUserAsync(
        long adminUserId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "Хэрэглэгч олдсонгүй.");
        }

        var oldValue = new
        {
            user.FailedLoginCount,
            user.LockedUntil,
            user.LockedUntilUtc,
            user.LockedUntilServerTick
        };

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LockedUntilUtc = null;
        user.LockedUntilServerTick = null;
        user.LastFailedLoginAt = null;
        user.LastFailedLoginAtUtc = null;
        user.LastFailedLoginServerTick = null;
        user.UpdatedAt = MongoliaClock.Now;

        AddAuditLog(
            adminUserId,
            "ADMIN_USER_UNLOCKED",
            "users",
            user.Id,
            oldValue,
            new
            {
                user.FailedLoginCount,
                user.LockedUntil,
                user.LockedUntilUtc,
                user.LockedUntilServerTick
            },
            $"Admin unlocked user {user.Username}.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Хэрэглэгчийн түгжээ амжилттай тайлагдлаа.");
    }

    public async Task<(bool Success, string? ErrorMessage)> SetUserActiveStatusAsync(
        long adminUserId,
        long userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (adminUserId == userId && !isActive)
        {
            return (false, "Өөрийн admin эрхийг идэвхгүй болгох боломжгүй.");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "Хэрэглэгч олдсонгүй.");
        }

        var oldValue = new { user.IsActive };
        user.IsActive = isActive;
        user.UpdatedAt = MongoliaClock.Now;

        AddAuditLog(
            adminUserId,
            "USER_STATUS_UPDATED",
            "users",
            user.Id,
            oldValue,
            new { user.IsActive },
            isActive
                ? $"User {user.Username} was enabled and sign-in access was restored."
                : $"User {user.Username} was disabled and sign-in access was blocked.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Хэрэглэгчийн төлөв амжилттай шинэчлэгдлээ.");
    }

    private static string? BuildFullName(string? firstName, string? lastName)
    {
        return UserDisplayNameFormatter.FormatOrNull(firstName, lastName);
    }
}
