using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Common.Extensions;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.Enums;
using Kavita.Services.Helpers;
using Kavita.Services.Scanner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Kobo;

public class KepubifyConverterService(
    ILogger<KepubifyConverterService> logger,
    IServiceScopeFactory serviceScopeFactory,
    IHostApplicationLifetime applicationLifetime): IKepubifyConverterService
{
    public const string KepubifyVersion = "v5.0.0";
    private const string VersionOutputPrefix = "kepubify ";
    private const string ProgressCost = "books sync to Kobo as plain epub, and reading progress only syncs to the chapter";

    // 3 times the 100 s measured for a 1.35 GB epub with 1000 chapters
    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VersionCheckTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// SHA-256 of every kepubify asset the release configs download, all <see cref="KepubifyVersion"/>
    /// </summary>
    internal static readonly IReadOnlySet<string> DefaultShippedHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "cb77fe364f780feea8de4586600f7160c7516586c21d9a80b1504c6630389069", // linux-64bit
        "6d6b44725f3161f3fa9f364c3c7cabd810a67e90420e18c84fe9f366c80d6782", // linux-arm64
        "57aa3e1a2d8e30af2ebce70e4c203889ee2b66d4b97c9d88a24b6f40b0de26a5", // linux-arm
        "3b833ee3caceb42894cc7e44a132c29a416de180666b0e49ebf8bca42fe6995d", // windows-64bit
        "8df1022c753423c1bab726eda567d96d0eae5c7dfece3c31b2f0817761b6b02b", // windows-32bit
        "bd9e40cb074d84c3081b3548656eb7e17117e29e577dc805252e931aa9aae5e6", // darwin-arm64
        "c007999e56f5135bd01fbb40ba888a17b08c16db9b098c7a8bfa8a1275df392d", // darwin-64bit
    };

    public static string ShippedFileName => OperatingSystem.IsWindows() ? "kepubify.exe" : "kepubify";

    internal IReadOnlySet<string> ShippedHashes { get; init; } = DefaultShippedHashes;
    internal Func<string> CustomPath { get; init; } = () => Configuration.KepubifyPath;

    private readonly ConcurrentDictionary<string, Lazy<Task<bool>>> _conversions = new();
    private readonly SemaphoreSlim _conversionSlots = new(2, 2);
    private readonly SemaphoreSlim _checkLock = new(1, 1);

    public KepubifyStatusDto Status { get; private set; } = KepubifyStatusDto.Checking;
    public bool KepubifySupported => Status.Status == KepubifyCheckStatus.Supported;

    public string BuildKepubifyPath(int chapterId)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var directoryService = scope.ServiceProvider.GetRequiredService<IDirectoryService>();

        var dateStr = DateTime.UtcNow.ToShortDateString().Replace("/", "_");
        var fileName = $"kavita_kepubify_c{chapterId}_{dateStr}.kepub.epub";
        return Path.Join(directoryService.TempDirectory, fileName);
    }

    public KepubifyStatusDto QueueCheck()
    {
        Status = KepubifyStatusDto.Checking;
        BackgroundJob.Enqueue<IKepubifyConverterService>(s => s.CheckKepubifySupport(CancellationToken.None));

        return Status;
    }

    public async Task<KepubifyStatusDto> CheckKepubifySupport(CancellationToken cancellationToken)
    {
        await _checkLock.WaitAsync(cancellationToken);
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var status = await RunCheck(scope.ServiceProvider, cancellationToken);
            Status = status;

            var eventHub = scope.ServiceProvider.GetRequiredService<IEventHub>();
            var message = MessageFactory.KepubifyCheckedEvent(status);
            await eventHub.SendMessageAsync(message.Name, message, true, cancellationToken);

            return status;
        }
        finally
        {
            _checkLock.Release();
        }
    }

    private async Task<KepubifyStatusDto> RunCheck(IServiceProvider services, CancellationToken cancellationToken)
    {
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var fileSystem = services.GetRequiredService<IFileSystem>();

        var customPath = CustomPath();
        var source = string.IsNullOrEmpty(customPath) ? KepubifySource.Shipped : KepubifySource.Custom;
        var path = source == KepubifySource.Custom ? customPath : fileSystem.Path.GetFullPath(ShippedFileName);

        var enabled = await unitOfWork.SettingsRepository.GetSettingAsync(ServerSettingKey.EnableKepubify, cancellationToken);
        if (!bool.Parse(enabled.Value))
        {
            return Off(source, path, KepubifyOffReason.Disabled, "unticked in settings");
        }

        if (!fileSystem.File.Exists(path))
        {
            return Off(source, path, KepubifyOffReason.FileMissing, $"{path.Sanitize()} does not exist");
        }

        if (source == KepubifySource.Shipped)
        {
            var hash = await HashFile(fileSystem, path, cancellationToken);
            if (!ShippedHashes.Contains(hash))
            {
                await unitOfWork.SettingsRepository.UpdateSettingAsync(ServerSettingKey.EnableKepubify, bool.FalseString, cancellationToken);

                logger.LogCritical(
                    "The shipped kepubify at {Path} has hash {Hash}, which does not match any {Version} release, so it was not run. " +
                    "The kepub setting was turned off and Kobo Sync keeps running on plain epubs. " +
                    "Replace the file or set KepubifyPath in appsettings.json, then enable the setting",
                    path.Sanitize(), hash, KepubifyVersion);

                return new KepubifyStatusDto(KepubifyCheckStatus.NotSupported, source, path, KepubifyOffReason.HashMismatch);
            }
        }

        var versionFailure = await CheckVersion(services, path, cancellationToken);
        if (versionFailure != null)
        {
            return Off(source, path, KepubifyOffReason.VersionCheckFailed, versionFailure);
        }

        logger.LogInformation("Kepub conversion is on, using the {Source} kepubify at {Path}", source, path.Sanitize());
        return new KepubifyStatusDto(KepubifyCheckStatus.Supported, source, path, null);
    }

    private KepubifyStatusDto Off(KepubifySource source, string path, KepubifyOffReason reason, string detail)
    {
        logger.LogWarning("Kepubify is off ({Detail}): {Cost}", detail, ProgressCost);
        return new KepubifyStatusDto(KepubifyCheckStatus.NotSupported, source, path, reason);
    }

    private static async Task<string> HashFile(IFileSystem fileSystem, string path, CancellationToken cancellationToken)
    {
        await using var stream = fileSystem.File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    /// <returns>Null when the binary printed a kepubify version, otherwise why it failed</returns>
    private static async Task<string?> CheckVersion(IServiceProvider services, string path, CancellationToken cancellationToken)
    {
        var processRunner = services.GetRequiredService<IProcessRunner>();
        var command = new ProcessCommand.Builder(processRunner)
            .WithTimeout(VersionCheckTimeout)
            .WithExecutable(path)
            .AppendArgument("--version")
            .Build();

        try
        {
            var result = await command.ExecuteAsync(cancellationToken);
            result.ThrowIfFailed();

            return result.StandardOutput.StartsWith(VersionOutputPrefix, StringComparison.Ordinal)
                ? null
                : $"{path.Sanitize()} --version did not print a kepubify version";
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return $"{path.Sanitize()} --version failed: {ex.Message.Sanitize()}";
        }
    }

    public async Task<bool> ConvertEpubToKepub(string epubPath, string kepubPath, CancellationToken cancellationToken)
    {
        if (!KepubifySupported)
        {
            logger.LogInformation("Kepub was requested for {FilePath} but kepubify is off, serving the plain epub", epubPath.Sanitize());
            return false;
        }

        var executable = Status.Path;
        var conversion = _conversions.GetOrAdd(kepubPath,
            key => new Lazy<Task<bool>>(() => RunConversion(executable, epubPath, key)));

        return await conversion.Value.WaitAsync(cancellationToken);
    }

    private async Task<bool> RunConversion(string executable, string epubPath, string kepubPath)
    {
        var stopping = applicationLifetime.ApplicationStopping;

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var processRunner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();
            var fileSystem = scope.ServiceProvider.GetRequiredService<IFileSystem>();

            if (fileSystem.File.Exists(kepubPath)) return true;

            await _conversionSlots.WaitAsync(stopping);
            try
            {
                var command = new ProcessCommand.Builder(processRunner)
                    .WithTimeout(ConversionTimeout)
                    .WithExecutable(executable)
                    .AppendArgument(epubPath)
                    .AppendArgument("-o")
                    .AppendArgument(kepubPath)
                    .Build();

                var result = await command.ExecuteAsync(stopping);
                result.ThrowIfFailed();
                return true;
            }
            finally
            {
                _conversionSlots.Release();
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            await ReportFailure(epubPath, $"Timed out after {ConversionTimeout.TotalMinutes} minutes");
            return false;
        }
        catch (Exception ex)
        {
            await ReportFailure(epubPath, ParseIssues.Describe(ex));
            return false;
        }
        finally
        {
            _conversions.TryRemove(kepubPath, out _);
        }
    }

    private async Task ReportFailure(string epubPath, string details)
    {
        logger.LogError("Kepub conversion failed for {FilePath}: {Details}", epubPath.Sanitize(), details.Sanitize());

        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var mediaErrorService = scope.ServiceProvider.GetRequiredService<IMediaErrorService>();
            await mediaErrorService.ReportMediaIssueAsync(epubPath, MediaErrorProducer.Kepubify,
                MediaErrorReason.KepubConversionFailed, details, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not save the media error for {FilePath}", epubPath.Sanitize());
        }
    }
}
