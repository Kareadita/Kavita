using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Services.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kavita.Services.Kobo;

public class KepubifyConverter(ILogger<KepubifyConverter> logger, IServiceScopeFactory serviceScopeFactory): IKepubifyConverter
{
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

    public async Task ConvertEpubToKepub(string epubPath, string kepubPath, CancellationToken cancellationToken)
    {
        if (!KepubifySupported)
        {
            throw new NotSupportedException("Kepubify is not supported");
        }

        using var scope = serviceScopeFactory.CreateScope();
        var processRunner = scope.ServiceProvider.GetRequiredService<IProcessRunner>();
        var fileSystem = scope.ServiceProvider.GetRequiredService<IFileSystem>();

        if (fileSystem.File.Exists(kepubPath))
        {
            return;
        }

        var command = new ProcessCommand.Builder(processRunner)
            .WithTimeout(TimeSpan.FromSeconds(10))
            .WithExecutable(_kepubifyPath)
            .AppendArgument(epubPath)
            .AppendArgument("-o")
            .AppendArgument(kepubPath)
            .Build();

        var result = await command.ExecuteAsync(cancellationToken);
        result.ThrowIfFailed();
    }
}
