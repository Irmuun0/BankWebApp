namespace BankWebApp.Web.Components.Charts;

public class ChartDataPoint
{
    public string Label { get; set; } = string.Empty;

    public decimal Value { get; set; }

    public string? Detail { get; set; }

    public string Color { get; set; } = "#2563eb";
}
