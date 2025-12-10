using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API.Services.HostedServices;

public class ReadingSessionInitializer : IHostedService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ReadingSessionInitializer> _logger;

    public ReadingSessionInitializer(IServiceScopeFactory serviceScopeFactory,
        ILogger<ReadingSessionInitializer> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ensuring all reading sessions are closed");

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();

        await context.AppUserReadingSession
            .Where(s => s.EndTime == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsActive, false)
                .SetProperty(x => x.EndTime, DateTime.Now)
                .SetProperty(x => x.EndTimeUtc, DateTime.UtcNow)
                .SetProperty(x => x.LastModified, DateTime.Now)
                .SetProperty(x => x.LastModifiedUtc, DateTime.UtcNow),
                cancellationToken);

        _logger.LogInformation("Partial reading sessions cleared");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
