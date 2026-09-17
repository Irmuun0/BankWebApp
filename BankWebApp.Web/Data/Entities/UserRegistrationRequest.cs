using System;

namespace BankWebApp.Web.Data.Entities;

public partial class UserRegistrationRequest
{
    public long Id { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string RequestedUsername { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public string NationalId { get; set; } = null!;

    public string? EmergencyPhoneNumber { get; set; }

    public string PreferredContactMethod { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? RequestNote { get; set; }

    public string? AdminNote { get; set; }

    public string? DecisionMessage { get; set; }

    public long? ReviewedByAdminId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public long? CreatedUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
