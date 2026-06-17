namespace BankWebApp.Web.Services.Interfaces;

public interface IDatabaseTestService
{
    Task<int> GetUserCountAsync();
}