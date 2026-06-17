namespace BankWebApp.Web.DTOs.Notifications;

public class NotificationDto
{
    public long Id { get; set; }
    public long? TransactionId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
