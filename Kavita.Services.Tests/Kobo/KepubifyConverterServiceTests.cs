using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Kavita.API.Services;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Misc;
using Kavita.Services.Kobo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests.Kobo;

public class KepubifyConverterServiceTests
{
    private const string EpubPath = "/books/a.epub";
    private const string KepubPath = "/temp/a.kepub.epub";

    private readonly MockFileSystem _fileSystem = new();
    private readonly IMediaErrorService _mediaErrorService = Substitute.For<IMediaErrorService>();
    private readonly StubProcessRunner _processRunner;

    public KepubifyConverterServiceTests()
    {
        _processRunner = new StubProcessRunner(_fileSystem);
    }

    private async Task<KepubifyConverterService> CreateService()
    {
        var provider = new ServiceCollection()
            .AddSingleton<IProcessRunner>(_processRunner)
            .AddSingleton<IFileSystem>(_fileSystem)
            .AddSingleton(_mediaErrorService)
            .BuildServiceProvider();

        var service = new KepubifyConverterService(
            Substitute.For<ILogger<KepubifyConverterService>>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<IHostApplicationLifetime>());

        await service.CheckKepubifySupport("kepubify", CancellationToken.None);

        return service;
    }

    [Fact]
    public async Task Convert_WritesOutput_ReturnsTrue()
    {
        var service = await CreateService();

        Assert.True(await service.ConvertEpubToKepub(EpubPath, KepubPath, CancellationToken.None));
        Assert.True(_fileSystem.File.Exists(KepubPath));
    }

    [Fact]
    public async Task Convert_TwoCallers_RunsOnce()
    {
        var service = await CreateService();
        _processRunner.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = service.ConvertEpubToKepub(EpubPath, KepubPath, CancellationToken.None);
        var second = service.ConvertEpubToKepub(EpubPath, KepubPath, CancellationToken.None);
        _processRunner.Gate.SetResult();

        Assert.True(await first);
        Assert.True(await second);
        Assert.Equal(1, _processRunner.ConversionRuns);
    }

    [Fact]
    public async Task Convert_RunnerTimesOut_ReturnsFalseAndReportsMediaError()
    {
        var service = await CreateService();
        _processRunner.TimesOut = true;

        Assert.False(await service.ConvertEpubToKepub(EpubPath, KepubPath, CancellationToken.None));

        await _mediaErrorService.Received(1).ReportMediaIssueAsync(EpubPath, MediaErrorProducer.Kepubify,
            MediaErrorReason.KepubConversionFailed, "Timed out after 5 minutes", Arg.Any<CancellationToken>());
    }

    private sealed class StubProcessRunner(MockFileSystem fileSystem) : IProcessRunner
    {
        public TaskCompletionSource? Gate { get; set; }
        public bool TimesOut { get; set; }
        public int ConversionRuns { get; private set; }

        public async Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments,
            string? workingDirectory, CancellationToken ct)
        {
            if (arguments.Contains("-o"))
            {
                ConversionRuns++;

                if (Gate != null)
                {
                    await Gate.Task.WaitAsync(ct);
                }

                if (TimesOut)
                {
                    throw new OperationCanceledException();
                }

                fileSystem.AddEmptyFile(arguments[^1]);
            }

            return new ProcessResult
            {
                Executable = executable,
                Arguments = arguments,
                ExitCode = 0,
                StandardOutput = string.Empty,
                StandardError = string.Empty,
            };
        }
    }
}
