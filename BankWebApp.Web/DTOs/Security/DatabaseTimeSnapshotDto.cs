namespace BankWebApp.Web.DTOs.Security;

public class DatabaseTimeSnapshotDto
{
    public DateTime UtcNow { get; set; }

    public long? ServerTickMilliseconds { get; set; }
}
