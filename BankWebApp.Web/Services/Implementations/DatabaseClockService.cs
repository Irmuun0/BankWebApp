using System.Data;
using BankWebApp.Web.Data;
using BankWebApp.Web.DTOs.Security;
using BankWebApp.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

public class DatabaseClockService : IDatabaseClockService
{
    private readonly BankDbContext _dbContext;

    public DatabaseClockService(BankDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DatabaseTimeSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT SYSUTCDATETIME(), CAST(ms_ticks AS BIGINT) FROM sys.dm_os_sys_info;";

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    return new DatabaseTimeSnapshotDto
                    {
                        UtcNow = DateTime.SpecifyKind(reader.GetDateTime(0), DateTimeKind.Utc),
                        ServerTickMilliseconds = reader.IsDBNull(1) ? null : reader.GetInt64(1)
                    };
                }
            }
            catch
            {
                // Some SQL Server users may not have permission to read dm_os_sys_info.
                // UTC from the database is still a better authority than the web server clock.
            }

            await using var fallbackCommand = connection.CreateCommand();
            fallbackCommand.CommandText = "SELECT SYSUTCDATETIME();";
            var utcNow = (DateTime)(await fallbackCommand.ExecuteScalarAsync(cancellationToken)
                ?? throw new InvalidOperationException("Database time query returned no value."));

            return new DatabaseTimeSnapshotDto
            {
                UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
                ServerTickMilliseconds = null
            };
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
