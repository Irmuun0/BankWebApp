using System.Text.Json;
using BankWebApp.Web.Data;
using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Profile;
using BankWebApp.Web.Helpers;
using BankWebApp.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

public class ProfileService : IProfileService
{
    private readonly BankDbContext _dbContext;
    private readonly IPasswordPolicyService _passwordPolicyService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProfileService(
        BankDbContext dbContext,
        IPasswordPolicyService passwordPolicyService,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _passwordPolicyService = passwordPolicyService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<UserProfileDto?> GetProfileAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

        return user is null ? null : MapProfile(user);
    }

    public async Task<ProfileUpdateResultDto> UpdateProfileAsync(
        long userId,
        UpdateProfileDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return Failed("Хэрэглэгч олдсонгүй.");
        }

        var username = dto.Username.Trim();
        var email = dto.Email.Trim();
        var phoneNumber = dto.PhoneNumber.Trim();
        var emergencyPhone = string.IsNullOrWhiteSpace(dto.EmergencyPhoneNumber)
            ? null
            : dto.EmergencyPhoneNumber.Trim();

        if (username.Length < 3)
        {
            return Failed("Нэвтрэх нэр хамгийн багадаа 3 тэмдэгттэй байх ёстой.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return Failed("И-мэйл хаяг буруу байна.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return Failed("Гар утасны дугаар хоосон байж болохгүй.");
        }

        var usernameExists = await _dbContext.Users
            .AnyAsync(item => item.Id != userId && item.Username == username, cancellationToken);
        if (usernameExists)
        {
            return Failed("Энэ нэвтрэх нэр бүртгэлтэй байна.");
        }

        var emailExists = await _dbContext.Users
            .AnyAsync(item => item.Id != userId && item.Email == email, cancellationToken);
        if (emailExists)
        {
            return Failed("Энэ и-мэйл хаяг бүртгэлтэй байна.");
        }

        var changingPassword = !string.IsNullOrWhiteSpace(dto.CurrentPassword)
            || !string.IsNullOrWhiteSpace(dto.NewPassword)
            || !string.IsNullOrWhiteSpace(dto.ConfirmPassword);

        if (changingPassword)
        {
            if (string.IsNullOrWhiteSpace(dto.CurrentPassword) ||
                !BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.PasswordHash))
            {
                return Failed("Одоогийн нууц үг буруу байна.");
            }

            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword != dto.ConfirmPassword)
            {
                return Failed("Шинэ нууц үг давталттайгаа таарахгүй байна.");
            }

            var policy = _passwordPolicyService.Validate(dto.NewPassword, username, email);
            if (!policy.IsValid)
            {
                return Failed(string.Join(" ", policy.Errors));
            }
        }

        var updatedAt = MongoliaClock.Now;
        var oldValue = new
        {
            user.Username,
            user.Email,
            user.PhoneNumber,
            user.EmergencyPhoneNumber,
            PasswordChanged = false
        };

        user.Username = username;
        user.Email = email;
        user.PhoneNumber = phoneNumber;
        user.EmergencyPhoneNumber = emergencyPhone;
        user.UpdatedAt = updatedAt;

        if (changingPassword)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.PasswordChangedAt = updatedAt;
        }

        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            Action = "PROFILE_UPDATED",
            TargetType = "users",
            TargetId = user.Id,
            OldValue = JsonSerializer.Serialize(oldValue),
            NewValue = JsonSerializer.Serialize(new
            {
                user.Username,
                user.Email,
                user.PhoneNumber,
                user.EmergencyPhoneNumber,
                PasswordChanged = changingPassword
            }),
            Detail = "User updated own profile settings.",
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString(),
            CreatedAt = updatedAt
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProfileUpdateResultDto
        {
            Success = true,
            Profile = MapProfile(user)
        };
    }

    private static UserProfileDto MapProfile(User user)
    {
        return new UserProfileDto
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            EmergencyPhoneNumber = user.EmergencyPhoneNumber,
            Role = user.Role,
            LastLoginAt = user.LastLoginAt,
            PasswordChangedAt = user.PasswordChangedAt
        };
    }

    private static ProfileUpdateResultDto Failed(string message)
    {
        return new ProfileUpdateResultDto
        {
            Success = false,
            ErrorMessage = message
        };
    }
}
