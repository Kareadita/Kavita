using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Common.Extensions;
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
    // 3 times the 100 s measured for a 1.35 GB epub with 1000 chapters
    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, Lazy<Task<bool>>> _conversions = new();
    private readonly SemaphoreSlim _conversionSlots = new(2, 2);

    private string _kepubifyPath = string.Empty;
    public bool KepubifySupported { get; private set; }

    public string BuildKepubifyPath(int chapterId)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var directoryService = scope.ServiceProvider.GetRequiredService<IDirectoryService>();

        var dateStr = DateTime.UtcNow.ToShortDateString().Replace("/", "_");
        var fileName = $"kavita_kepubify_c{chapterId}_{dateStr}.kepub.epub";
        return Path.Join(directoryService.TempDirectory, fileName);
    }

    public async Task CheckKepubifySupport(string kepubifyPath, CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var processRunner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();

        if (string.IsNullOrEmpty(kepubifyPath))
        {
            logger.LogWarning("No Kepubify path was provided... kepub conversion will not be supported");
            KepubifySupported = false;
            _kepubifyPath = string.Empty;
            return;
        }

        var checkCommand = new ProcessCommand.Builder(processRunner)
            .WithTimeout(TimeSpan.FromSeconds(3))
            .WithExecutable(kepubifyPath)
            .Build();

        try
        {
            var result = await checkCommand.ExecuteAsync(cancellationToken);
            result.ThrowIfFailed();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Kavita cannot run Kepubify... kepub conversion will not be supported");
            KepubifySupported = false;
            _kepubifyPath = string.Empty;
            return;
        }

        KepubifySupported = true;
        _kepubifyPath = kepubifyPath;
    }

    public async Task<bool> ConvertEpubToKepub(string epubPath, string kepubPath, CancellationToken cancellationToken)
    {
        if (!KepubifySupported) return false;

        var conversion = _conversions.GetOrAdd(kepubPath,
            _ => new Lazy<Task<bool>>(() => RunConversion(epubPath, kepubPath)));

        return await conversion.Value.WaitAsync(cancellationToken);
    }

    private async Task<bool> RunConversion(string epubPath, string kepubPath)
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
                    .WithExecutable(_kepubifyPath)
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
