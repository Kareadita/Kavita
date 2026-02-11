using Kavita.API.Database;
using Microsoft.Extensions.DependencyInjection;

namespace Kavita.Database.Extensions;

public static class ApplicationServiceExtensions
{
    public static void AddKavitaDatabases(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDataContext, DataContext>();
    }
}
