namespace BankWebApp.Web.DTOs.Admin;

public class AdminAiDetectionPageDto
{
    public AdminPagedResultDto<AdminAiDetectionTransactionDto> Transactions { get; set; } = new();
    public AdminAiDetectionTransactionDto? ChatTransaction { get; set; }
    public List<AdminAiDetectionChatMessageDto> ChatMessages { get; set; } = [];
}
