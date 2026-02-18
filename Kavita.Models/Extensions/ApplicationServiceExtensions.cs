using Microsoft.Extensions.DependencyInjection;

namespace Kavita.Models.Extensions;

public static class ApplicationServiceExtensions
{
    public static void AddMappings(this IServiceCollection services)
    {
        services.AddAutoMapper(typeof(ApplicationServiceExtensions).Assembly);
    }
}
