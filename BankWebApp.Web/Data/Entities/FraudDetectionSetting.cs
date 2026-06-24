namespace BankWebApp.Web.Data.Entities;

public partial class FraudDetectionSetting
{
    public int Id { get; set; }

    public int SuspiciousThreshold { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual User? UpdatedByNavigation { get; set; }
}
