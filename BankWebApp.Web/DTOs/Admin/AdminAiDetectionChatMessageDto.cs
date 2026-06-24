namespace BankWebApp.Web.DTOs.Admin;

public class AdminAiDetectionChatMessageDto
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
