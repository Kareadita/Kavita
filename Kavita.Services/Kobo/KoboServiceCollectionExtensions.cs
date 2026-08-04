using Kavita.API.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Kavita.Services.Kobo;

public static class KoboServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Kobo sync surface: the facade service plus conversion, kepubify, scheduling,
    /// and location-mapping collaborators.
    /// </summary>
    public static IServiceCollection AddKoboServices(this IServiceCollection services)
    {
        services.AddScoped<IKoboService, KoboService>();
        services.AddScoped<IKoboArchiveEpubConverter, KoboArchiveEpubConverter>();
        services.AddSingleton<IKepubifyPathResolver, KepubifyPathResolver>();
        services.AddScoped<IKepubifyRunner, KepubifyRunner>();
        services.AddScoped<IKoboConversionJobScheduler, HangfireKoboConversionJobScheduler>();
        services.AddScoped<IKoboConversionService, KoboConversionService>();
        services.AddScoped<IKoboLocationMapper, KoboLocationMapper>();
        services.AddScoped<IKoboLocationRematchService, KoboLocationRematchService>();
        services.AddScoped<IKoboConvertProgressLocationService, KoboConvertProgressLocationService>();
        return services;
    }
}
