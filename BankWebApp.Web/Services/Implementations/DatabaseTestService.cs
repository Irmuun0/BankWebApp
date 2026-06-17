using BankWebApp.Web.Data;
using BankWebApp.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

public class DatabaseTestService : IDatabaseTestService
{
    private readonly BankDbContext _context;

    public DatabaseTestService(BankDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetUserCountAsync()
    {
        return await _context.Users.CountAsync();
    }
}