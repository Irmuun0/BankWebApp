namespace BankWebApp.Web.DTOs.Health;

public class OperationalHealthComponentDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public bool IsCritical { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? Endpoint { get; set; }
    public long DurationMs { get; set; }
    public DateTime CheckedAt { get; set; }
}
