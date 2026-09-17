using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using BankWebApp.Web.Data;
using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.DTOs.Registration;
using BankWebApp.Web.Helpers;
using BankWebApp.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

public class UserRegistrationService : IUserRegistrationService
{
    private static readonly Regex MongolianNameRegex = new(@"^[А-Яа-яЁёӨөҮү\s]+$", RegexOptions.Compiled);
    private static readonly Regex EnglishUsernameRegex = new(@"^[A-Za-z]+$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"^\d{8}$", RegexOptions.Compiled);
    private static readonly Regex NationalIdRegex = new(@"^[А-ЯЁӨҮ]{2}\d{8}$", RegexOptions.Compiled);

    private const string StatusPending = "PENDING";
    private const string StatusApproved = "APPROVED";
    private const string StatusRejected = "REJECTED";
    private const string StatusNeedsInfo = "NEEDS_INFO";

    private readonly BankDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserRegistrationService(BankDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<(bool Success, string? ErrorMessage)> SubmitRequestAsync(
        CreateRegistrationRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeCreateDto(dto);
        var validationError = await ValidateNewRequestAsync(normalized, cancellationToken);
        if (validationError is not null)
        {
            return (false, validationError);
        }

        var now = MongoliaClock.Now;
        _dbContext.UserRegistrationRequests.Add(new UserRegistrationRequest
        {
            FirstName = normalized.FirstName,
            LastName = normalized.LastName,
            RequestedUsername = normalized.RequestedUsername,
            Email = normalized.Email,
            PhoneNumber = normalized.PhoneNumber,
            NationalId = normalized.NationalId,
            EmergencyPhoneNumber = normalized.EmergencyPhoneNumber,
            PreferredContactMethod = normalized.PreferredContactMethod,
            RequestNote = normalized.RequestNote,
            Status = StatusPending,
            CreatedAt = now,
            UpdatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Бүртгэл үүсгэх хүсэлт амжилттай илгээгдлээ. Админ хянаж баталгаажуулсны дараа хариу илгээгдэнэ.");
    }

    public async Task<AdminPagedResultDto<AdminRegistrationRequestDto>> GetRequestsAsync(
        AdminRegistrationRequestFilterDto? filter = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = pageSize is 50 or 100 ? pageSize : 20;
        var query = _dbContext.UserRegistrationRequests.AsNoTracking();

        var search = NormalizeSearch(filter?.Search);
        if (search is not null)
        {
            query = query.Where(item =>
                item.FirstName.Contains(search) ||
                item.LastName.Contains(search) ||
                item.RequestedUsername.Contains(search) ||
                item.Email.Contains(search) ||
                item.PhoneNumber.Contains(search) ||
                item.NationalId.Contains(search));
        }

        var status = NormalizeStatus(filter?.Status);
        if (status is not null)
        {
            query = query.Where(item => item.Status == status);
        }

        if (filter?.CreatedFrom is not null)
        {
            var start = filter.CreatedFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(item => item.CreatedAt >= start);
        }

        if (filter?.CreatedTo is not null)
        {
            var end = filter.CreatedTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(item => item.CreatedAt < end);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(item => MapRequest(item))
            .ToListAsync(cancellationToken);

        return new AdminPagedResultDto<AdminRegistrationRequestDto>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = total
        };
    }

    public async Task<AdminRegistrationRequestDto?> GetRequestAsync(long requestId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserRegistrationRequests
            .AsNoTracking()
            .Where(item => item.Id == requestId)
            .Select(item => MapRequest(item))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RegistrationReviewResultDto> ReviewRequestAsync(
        long adminUserId,
        ReviewRegistrationRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.UserRegistrationRequests
            .FirstOrDefaultAsync(item => item.Id == dto.RequestId, cancellationToken);

        if (request is null)
        {
            return RegistrationReviewResultDto.Failed("Бүртгэлийн хүсэлт олдсонгүй.");
        }

        if (request.Status == StatusApproved)
        {
            return RegistrationReviewResultDto.Failed("Энэ хүсэлт аль хэдийн зөвшөөрөгдсөн байна.");
        }

        var decision = NormalizeDecision(dto.Decision);
        if (decision is null)
        {
            return RegistrationReviewResultDto.Failed("Review шийдвэр буруу байна.");
        }

        if (decision == StatusApproved)
        {
            return await ApproveRequestAsync(adminUserId, request, dto, cancellationToken);
        }

        var now = MongoliaClock.Now;
        var oldValue = new { request.Status, request.AdminNote, request.DecisionMessage };
        request.Status = decision;
        request.AdminNote = NormalizeOptional(dto.AdminNote);
        request.DecisionMessage = NormalizeOptional(dto.DecisionMessage) ?? BuildDefaultDecisionMessage(decision);
        request.ReviewedByAdminId = adminUserId;
        request.ReviewedAt = now;
        request.UpdatedAt = now;

        AddAuditLog(
            adminUserId,
            decision == StatusRejected ? "REGISTRATION_REQUEST_REJECTED" : "REGISTRATION_REQUEST_NEEDS_INFO",
            "user_registration_requests",
            request.Id,
            oldValue,
            new { request.Status, request.AdminNote, request.DecisionMessage },
            $"Admin reviewed registration request #{request.Id} as {request.Status}.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return RegistrationReviewResultDto.Completed(
            decision == StatusRejected
                ? "Бүртгэлийн хүсэлт татгалзлаа."
                : "Нэмэлт мэдээлэл шаардах төлөвт орууллаа.");
    }

    private async Task<RegistrationReviewResultDto> ApproveRequestAsync(
        long adminUserId,
        UserRegistrationRequest request,
        ReviewRegistrationRequestDto dto,
        CancellationToken cancellationToken)
    {
        var duplicateError = await ValidateApproveAsync(request, cancellationToken);
        if (duplicateError is not null)
        {
            return RegistrationReviewResultDto.Failed(duplicateError);
        }

        var now = MongoliaClock.Now;
        var temporaryPassword = GenerateTemporaryPassword();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var user = new User
        {
            Username = request.RequestedUsername,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            NationalId = request.NationalId,
            EmergencyPhoneNumber = request.EmergencyPhoneNumber,
            Role = "USER",
            IsActive = true,
            PasswordResetRequired = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var oldValue = new { request.Status, request.CreatedUserId };
        request.Status = StatusApproved;
        request.AdminNote = NormalizeOptional(dto.AdminNote);
        request.DecisionMessage = BuildApprovedDecisionMessage(NormalizeOptional(dto.DecisionMessage));
        request.ReviewedByAdminId = adminUserId;
        request.ReviewedAt = now;
        request.CreatedUserId = user.Id;
        request.UpdatedAt = now;

        _dbContext.Notifications.Add(new Notification
        {
            UserId = user.Id,
            NotificationType = "SYSTEM",
            Title = "Бүртгэл баталгаажлаа",
            Message = "Phoebe Bank таны бүртгэлийг баталгаажууллаа. Анхны нэвтрэлтээр нууц үгээ шинэчилнэ үү.",
            IsRead = false,
            CreatedAt = now
        });

        AddAuditLog(
            adminUserId,
            "REGISTRATION_REQUEST_APPROVED",
            "user_registration_requests",
            request.Id,
            oldValue,
            new { request.Status, request.CreatedUserId },
            $"Admin approved registration request #{request.Id} and created user {user.Username}.");

        AddAuditLog(
            adminUserId,
            "USER_CREATED_FROM_REGISTRATION_REQUEST",
            "users",
            user.Id,
            new { },
            new
            {
                user.Username,
                user.Email,
                user.Role,
                user.IsActive,
                user.PasswordResetRequired,
                RegistrationRequestId = request.Id
            },
            $"Admin created user {user.Username} from registration request #{request.Id}.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new RegistrationReviewResultDto
        {
            Success = true,
            Message = "Бүртгэлийн хүсэлт зөвшөөрөгдөж хэрэглэгч үүслээ. Түр нууц үгийг зөвхөн энэ удаа харуулна.",
            TemporaryPassword = temporaryPassword,
            Username = user.Username
        };
    }

    private async Task<string?> ValidateNewRequestAsync(CreateRegistrationRequestDto dto, CancellationToken cancellationToken)
    {
        if (dto.FirstName.Length < 2 || dto.LastName.Length < 2)
        {
            return "Нэр, овог хамгийн багадаа 2 тэмдэгттэй байх ёстой.";
        }

        if (!MongolianNameRegex.IsMatch(dto.FirstName) || !MongolianNameRegex.IsMatch(dto.LastName))
        {
            return "Нэр, овог талбарт зөвхөн монгол кирилл үсэг оруулна уу.";
        }

        if (dto.RequestedUsername.Length < 3 || !EnglishUsernameRegex.IsMatch(dto.RequestedUsername))
        {
            return "Нэвтрэх нэр зөвхөн англи үсэгтэй, хамгийн багадаа 3 тэмдэгттэй байх ёстой.";
        }

        if (string.IsNullOrWhiteSpace(dto.Email) || !EmailRegex.IsMatch(dto.Email))
        {
            return "И-мэйл хаяг зөвхөн англи үсэг, тоо болон email тэмдэгтүүдтэй байх ёстой.";
        }

        if (!PhoneRegex.IsMatch(dto.PhoneNumber))
        {
            return "Утасны дугаар яг 8 оронтой тоо байх ёстой.";
        }

        if (!string.IsNullOrWhiteSpace(dto.EmergencyPhoneNumber) && !PhoneRegex.IsMatch(dto.EmergencyPhoneNumber))
        {
            return "Яаралтай холбоо барих утас яг 8 оронтой тоо байх ёстой.";
        }

        if (!NationalIdRegex.IsMatch(dto.NationalId))
        {
            return "Регистрийн дугаарын эхний 2 тэмдэгт монгол үсэг, дараагийн 8 тэмдэгт тоо байх ёстой.";
        }

        if (await _dbContext.Users.AnyAsync(user => user.Username == dto.RequestedUsername, cancellationToken))
        {
            return "Энэ нэвтрэх нэр бүртгэлтэй байна.";
        }

        if (await _dbContext.Users.AnyAsync(user => user.Email == dto.Email, cancellationToken))
        {
            return "Энэ и-мэйл хаяг бүртгэлтэй байна.";
        }

        if (await _dbContext.Users.AnyAsync(user => user.NationalId == dto.NationalId, cancellationToken))
        {
            return "Энэ бүртгэлийн дугаараар хэрэглэгч бүртгэлтэй байна.";
        }

        var pendingStatuses = new[] { StatusPending, StatusNeedsInfo };
        var hasOpenRequest = await _dbContext.UserRegistrationRequests.AnyAsync(request =>
            pendingStatuses.Contains(request.Status) &&
            (request.RequestedUsername == dto.RequestedUsername ||
             request.Email == dto.Email ||
             request.NationalId == dto.NationalId),
            cancellationToken);

        return hasOpenRequest
            ? "Энэ мэдээллээр бүртгэлийн нээлттэй хүсэлт аль хэдийн байна."
            : null;
    }

    private async Task<string?> ValidateApproveAsync(UserRegistrationRequest request, CancellationToken cancellationToken)
    {
        if (await _dbContext.Users.AnyAsync(user => user.Username == request.RequestedUsername, cancellationToken))
        {
            return "Approve хийх боломжгүй: нэвтрэх нэр аль хэдийн бүртгэлтэй байна.";
        }

        if (await _dbContext.Users.AnyAsync(user => user.Email == request.Email, cancellationToken))
        {
            return "Approve хийх боломжгүй: и-мэйл хаяг аль хэдийн бүртгэлтэй байна.";
        }

        if (await _dbContext.Users.AnyAsync(user => user.NationalId == request.NationalId, cancellationToken))
        {
            return "Approve хийх боломжгүй: бүртгэлийн дугаар аль хэдийн бүртгэлтэй байна.";
        }

        return null;
    }

    private static CreateRegistrationRequestDto NormalizeCreateDto(CreateRegistrationRequestDto dto)
    {
        return new CreateRegistrationRequestDto
        {
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            RequestedUsername = dto.RequestedUsername.Trim(),
            Email = dto.Email.Trim().ToLowerInvariant(),
            PhoneNumber = dto.PhoneNumber.Trim(),
            NationalId = dto.NationalId.Trim().ToUpperInvariant(),
            EmergencyPhoneNumber = NormalizeOptional(dto.EmergencyPhoneNumber),
            PreferredContactMethod = NormalizeContactMethod(dto.PreferredContactMethod),
            RequestNote = NormalizeOptional(dto.RequestNote)
        };
    }

    private static AdminRegistrationRequestDto MapRequest(UserRegistrationRequest item)
    {
        return new AdminRegistrationRequestDto
        {
            Id = item.Id,
            FirstName = item.FirstName,
            LastName = item.LastName,
            RequestedUsername = item.RequestedUsername,
            Email = item.Email,
            PhoneNumber = item.PhoneNumber,
            NationalId = item.NationalId,
            EmergencyPhoneNumber = item.EmergencyPhoneNumber,
            PreferredContactMethod = item.PreferredContactMethod,
            Status = item.Status,
            RequestNote = item.RequestNote,
            AdminNote = item.AdminNote,
            DecisionMessage = item.DecisionMessage,
            CreatedUserId = item.CreatedUserId,
            ReviewedAt = item.ReviewedAt,
            CreatedAt = item.CreatedAt
        };
    }

    private static string GenerateTemporaryPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        Span<byte> bytes = stackalloc byte[10];
        RandomNumberGenerator.Fill(bytes);

        var chars = bytes
            .ToArray()
            .Select(value => alphabet[value % alphabet.Length])
            .ToArray();

        return $"Tmp-{new string(chars)}!7";
    }

    private static string BuildApprovedDecisionMessage(string? adminMessage)
    {
        const string credentialMessage = "Your Phoebe Bank internet banking account has been approved. The temporary sign-in credential was issued through the selected delivery workflow. The password must be changed after first sign-in.";
        return string.IsNullOrWhiteSpace(adminMessage)
            ? credentialMessage
            : $"{adminMessage.Trim()}{Environment.NewLine}{Environment.NewLine}{credentialMessage}";
    }

    private static string BuildDefaultDecisionMessage(string decision)
    {
        return decision == StatusNeedsInfo
            ? "Phoebe Bank needs additional information before creating your internet banking account."
            : "Phoebe Bank cannot approve your internet banking registration request at this time.";
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeSearch(string? search)
    {
        var normalized = search?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeContactMethod(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return normalized is "PHONE" or "EMAIL" ? normalized : "EMAIL";
    }

    private static string? NormalizeStatus(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return normalized is StatusPending or StatusApproved or StatusRejected or StatusNeedsInfo ? normalized : null;
    }

    private static string? NormalizeDecision(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        return normalized is StatusApproved or StatusRejected or StatusNeedsInfo ? normalized : null;
    }

    private void AddAuditLog(
        long adminUserId,
        string action,
        string targetType,
        long targetId,
        object oldValue,
        object newValue,
        string detail)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            OldValue = JsonSerializer.Serialize(oldValue),
            NewValue = JsonSerializer.Serialize(newValue),
            Detail = detail,
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString(),
            CreatedAt = MongoliaClock.Now
        });
    }
}
