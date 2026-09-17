namespace BankWebApp.Web.DTOs.Health;

public class OperationalHealthReportDto
{
    public string Status { get; set; } = "Unknown";
    public DateTime GeneratedAt { get; set; }
    public long DurationMs { get; set; }
    public string Environment { get; set; } = string.Empty;
    public string Application { get; set; } = "Phoebe Bank Web";
    public IReadOnlyList<OperationalHealthComponentDto> Components { get; set; } = [];

    public int HealthyCount => Components.Count(component => component.Status == HealthStatusNames.Healthy);
    public int WarningCount => Components.Count(component => component.Status == HealthStatusNames.Warning);
    public int UnhealthyCount => Components.Count(component => component.Status == HealthStatusNames.Unhealthy);
}

public static class HealthStatusNames
{
    public const string Healthy = "Healthy";
    public const string Warning = "Warning";
    public const string Unhealthy = "Unhealthy";
    public const string Degraded = "Degraded";
}
