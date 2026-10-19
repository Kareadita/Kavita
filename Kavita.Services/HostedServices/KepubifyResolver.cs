using System;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Models.Entities.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.HostedServices;

internal class KepubifyResolver(IServiceScopeFactory scopeFactory): IHostedService
{
    private const string DefaultWindowsPath = "kepubify.exe";
    private const string DefaultUnixLikePath = "kepubify";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();

        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<KepubifyResolver>>();
        var kepubifyConverter = scope.ServiceProvider.GetRequiredService<IKepubifyConverter>();
        var fileSystem = scope.ServiceProvider.GetRequiredService<IFileSystem>();

        var kepubifyPath = (await unitOfWork.SettingsRepository
            .GetSettingAsync(ServerSettingKey.KepubifyPath, cancellationToken))
            .Value;

        // Do not overwrite user configured paths
        if (!string.IsNullOrEmpty(kepubifyPath) && !kepubifyPath.Equals(DefaultUnixLikePath) &&
            !kepubifyPath.Equals(DefaultWindowsPath))
        {
            logger.LogTrace("User has configured a custom kepubify path, will not reconcile");
            return;
        }

        var expectedPath = OperatingSystem.IsWindows() ?  DefaultWindowsPath : DefaultUnixLikePath;
        if (!kepubifyPath.Equals(expectedPath, StringComparison.OrdinalIgnoreCase))
        {
            if (fileSystem.File.Exists(expectedPath))
            {
                logger.LogInformation("Expected Kepubify path ({Expected}) differs from configured ({Configured})? Will update",
                    expectedPath, kepubifyPath);
                kepubifyPath = expectedPath;
            }
            else
            {
                logger.LogWarning("Expected Kepubify path ({Expected}) is not present. Not updating, Kepubify may not work!",
                    expectedPath);
            }

            await unitOfWork.SettingsRepository.UpdateSettingAsync(ServerSettingKey.KepubifyPath, kepubifyPath, cancellationToken);
        }

        await kepubifyConverter.CheckKepubifySupport(kepubifyPath, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
