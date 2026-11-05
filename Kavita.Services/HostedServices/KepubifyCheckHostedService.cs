using System;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.HostedServices;

internal class KepubifyCheckHostedService(
    IKepubifyConverterService kepubifyConverter,
    ILogger<KepubifyCheckHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await kepubifyConverter.CheckKepubifySupport(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The startup kepubify check failed, kepub conversion stays off");
        }
    }
}
