using Kavita.API.Database;
using Kavita.Common.EnvironmentInfo;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NeoSmart.Caching.Sqlite;

namespace Kavita.Database.Extensions;

public static class ApplicationServiceExtensions
{
    public static void AddKavitaDatabases(this IServiceCollection services)
    {
        services.AddSqLite();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDataContext, DataContext>();

        // Store keys inside database, such that cookies can be decrypted between container restarts
        services.AddDataProtection()
            .PersistKeysToDbContext<DataContext>()
            .SetApplicationName(BuildInfo.AppName);
    }

    private static void AddSqLite(this IServiceCollection services)
    {
        services.AddSqliteCache("config/cache.db");

        services.AddDbContextPool<DataContext>(options =>
        {
            options.UseSqlite("Data source=config/kavita.db", builder =>
            {
                builder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });
            options.EnableDetailedErrors();
            options.EnableSensitiveDataLogging();
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });
    }
}
