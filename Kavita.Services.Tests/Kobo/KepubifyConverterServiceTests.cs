using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Database.Tests;
using Kavita.Models.DTOs.Settings;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Misc;
using Kavita.Services.Kobo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Kavita.Services.Tests.Kobo;

public partial class KepubifyConverterServiceTests : AbstractFsTest
{
    private const string EpubPath = "/books/a.epub";
    private const string KepubPath = "/temp/a.kepub.epub";
    private static readonly byte[] ShippedBytes = "shipped kepubify"u8.ToArray();
    private static readonly string ShippedHash = Convert.ToHexStringLower(SHA256.HashData(ShippedBytes));
    private static readonly string CustomBinary = Root + "opt/kepubify";

    private readonly MockFileSystem _fileSystem = CreateFileSystem();
    private readonly IMediaErrorService _mediaErrorService = Substitute.For<IMediaErrorService>();
    private readonly ISettingsRepository _settingsRepository = Substitute.For<ISettingsRepository>();
    private readonly IEventHub _eventHub = Substitute.For<IEventHub>();
    private readonly StubProcessRunner _processRunner;
    private readonly string _shippedPath;
    private string _customPath = string.Empty;
    private string _enableKepubify = "true";

    public KepubifyConverterServiceTests()
    {
        _processRunner = new StubProcessRunner(_fileSystem);
        _shippedPath = _fileSystem.Path.GetFullPath(KepubifyConverterService.ShippedFileName);
        _fileSystem.AddFile(_shippedPath, new MockFileData(ShippedBytes));

        _settingsRepository.GetSettingAsync(ServerSettingKey.EnableKepubify, Arg.Any<CancellationToken>())
            .Returns(_ => new ServerSetting { Key = ServerSettingKey.EnableKepubify, Value = _enableKepubify });
    }

    private KepubifyConverterService CreateService()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SettingsRepository.Returns(_settingsRepository);

        var provider = new ServiceCollection()
            .AddSingleton<IProcessRunner>(_processRunner)
            .AddSingleton<IFileSystem>(_fileSystem)
            .AddSingleton(_mediaErrorService)
            .AddSingleton(unitOfWork)
            .AddSingleton(_eventHub)
            .BuildServiceProvider();

        return new KepubifyConverterService(
            Substitute.For<ILogger<KepubifyConverterService>>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            Substitute.For<IHostApplicationLifetime>())
        {
            ShippedHashes = new HashSet<string>([ShippedHash], StringComparer.OrdinalIgnoreCase),
            CustomPath = () => _customPath,
        };
    }

    private async Task<KepubifyConverterService> CreateCheckedService()
    {
        var service = CreateService();
        await service.CheckKepubifySupport(CancellationToken.None);
        return service;
    }

    [Fact]
    public async Task Convert_WritesOutput_ReturnsTrue()
    {
        var service = await CreateCheckedService();

        Assert.True(await service.ConvertEpubToKepub(EpubPath, KepubPath, CancellationToken.None));
        Assert.True(_fileSystem.File.Exists(KepubPath));
    }

    [Fact]
    public async Task Convert_TwoCallers_RunsOnce()
    {
        var service = await CreateCheckedService();
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
        var service = await CreateCheckedService();
        _processRunner.TimesOut = true;

        Assert.False(await service.ConvertEpubToKepub(EpubPath, KepubPath, CancellationToken.None));

        await _mediaErrorService.Received(1).ReportMediaIssueAsync(EpubPath, MediaErrorProducer.Kepubify,
            MediaErrorReason.KepubConversionFailed, "Timed out after 5 minutes", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Check_ShippedWrongHash_DisablesKepubifyNotKobo_NeverRuns()
    {
        _fileSystem.AddFile(_shippedPath, new MockFileData("changed"u8.ToArray()));

        var service = await CreateCheckedService();

        Assert.Equal(new KepubifyStatusDto(KepubifyCheckStatus.NotSupported, KepubifySource.Shipped, _shippedPath,
            KepubifyOffReason.HashMismatch), service.Status);
        await _settingsRepository.Received(1).UpdateSettingAsync(ServerSettingKey.EnableKepubify, "False", Arg.Any<CancellationToken>());
        await _settingsRepository.DidNotReceive().UpdateSettingAsync(ServerSettingKey.EnableKobo, Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Empty(_processRunner.Executables);
    }

    [Fact]
    public async Task Check_CustomPath_NotHashed_PassesOnVersion()
    {
        _customPath = CustomBinary;
        _fileSystem.AddFile(CustomBinary, new MockFileData("a musl build"u8.ToArray()));

        var service = await CreateCheckedService();
        await service.ConvertEpubToKepub(EpubPath, KepubPath, CancellationToken.None);

        Assert.Equal(new KepubifyStatusDto(KepubifyCheckStatus.Supported, KepubifySource.Custom, CustomBinary, null), service.Status);
        Assert.Equal([CustomBinary, CustomBinary], _processRunner.Executables);
    }

    [Fact]
    public async Task Check_CustomWrongVersionOutput_KepubOff_NoFallback()
    {
        _customPath = CustomBinary;
        _fileSystem.AddFile(CustomBinary, new MockFileData("whoami"u8.ToArray()));
        _processRunner.VersionOutput = "joe\n";

        var service = await CreateCheckedService();

        Assert.Equal(new KepubifyStatusDto(KepubifyCheckStatus.NotSupported, KepubifySource.Custom, CustomBinary,
            KepubifyOffReason.VersionCheckFailed), service.Status);
        Assert.DoesNotContain(_shippedPath, _processRunner.Executables);
    }

    [Fact]
    public async Task Check_CustomMissing_KepubOff_ShippedNeverRun()
    {
        _customPath = CustomBinary;

        var service = await CreateCheckedService();

        Assert.Equal(new KepubifyStatusDto(KepubifyCheckStatus.NotSupported, KepubifySource.Custom, CustomBinary,
            KepubifyOffReason.FileMissing), service.Status);
        Assert.Empty(_processRunner.Executables);
    }

    [Fact]
    public async Task Check_ShippedMissing_KepubOff_SettingsUntouched()
    {
        _fileSystem.RemoveFile(_shippedPath);

        var service = await CreateCheckedService();

        Assert.Equal(KepubifyOffReason.FileMissing, service.Status.Reason);
        await _settingsRepository.DidNotReceiveWithAnyArgs().UpdateSettingAsync(default, default!, default);
        Assert.Empty(_processRunner.Executables);
    }

    [Fact]
    public async Task Check_EnableKepubifyOff_NeverHashesOrRuns()
    {
        _enableKepubify = "False";
        _fileSystem.AddFile(_shippedPath, new MockFileData("changed"u8.ToArray()));

        var service = await CreateCheckedService();

        Assert.Equal(KepubifyOffReason.Disabled, service.Status.Reason);
        await _settingsRepository.DidNotReceiveWithAnyArgs().UpdateSettingAsync(default, default!, default);
        Assert.Empty(_processRunner.Executables);
    }

    [Fact]
    public void Status_IsChecking_BeforeFirstCheck()
    {
        var service = CreateService();

        Assert.Equal(KepubifyCheckStatus.Checking, service.Status.Status);
        Assert.False(service.KepubifySupported);
    }

    [Fact]
    public async Task Check_SendsCheckedEvent_ErrorOnFailure_SilentOtherwise()
    {
        var sent = new List<SignalRMessageDto>();
        _eventHub.When(h => h.SendMessageAsync(Arg.Any<string>(), Arg.Any<SignalRMessageDto>(), true, Arg.Any<CancellationToken>()))
            .Do(c => sent.Add(c.ArgAt<SignalRMessageDto>(1)));

        await CreateCheckedService();
        _enableKepubify = "False";
        await CreateCheckedService();
        _enableKepubify = "true";
        _fileSystem.AddFile(_shippedPath, new MockFileData("changed"u8.ToArray()));
        await CreateCheckedService();

        Assert.All(sent, m => Assert.Equal(MessageEventCode.KepubifyChecked, m.Code));
        Assert.Equal([MessageEventPriority.Silent, MessageEventPriority.Silent, MessageEventPriority.Error],
            sent.Select(m => m.Priority));
        await _eventHub.DidNotReceive().SendMessageAsync(Arg.Any<string>(), Arg.Any<SignalRMessageDto>(), false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ReleaseConfigs_Sha256_MatchHashList()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Join(repoRoot.FullName, "local-release.yml")))
        {
            repoRoot = repoRoot.Parent ?? throw new FileNotFoundException("local-release.yml not found above the test output");
        }

        var release = Sha256Values(Path.Join(repoRoot.FullName, "release.yml"));
        var localRelease = Sha256Values(Path.Join(repoRoot.FullName, "local-release.yml"));
        var hashList = KepubifyConverterService.DefaultShippedHashes;

        Assert.All(release.Concat(localRelease), hash => Assert.Contains(hash, hashList));
        Assert.All(hashList, hash => Assert.Contains(hash, localRelease));
    }

    private static HashSet<string> Sha256Values(string file)
    {
        return Sha256Regex().Matches(File.ReadAllText(file))
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .ToHashSet();
    }

    [GeneratedRegex("\"Sha256\":\\s*\"([0-9a-fA-F]{64})\"")]
    private static partial Regex Sha256Regex();

    private sealed class StubProcessRunner(MockFileSystem fileSystem) : IProcessRunner
    {
        public TaskCompletionSource? Gate { get; set; }
        public bool TimesOut { get; set; }
        public string VersionOutput { get; set; } = "kepubify v4.0.4\n";
        public int ConversionRuns { get; private set; }
        public List<string> Executables { get; } = [];

        public async Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments,
            string? workingDirectory, CancellationToken ct)
        {
            Executables.Add(executable);
            var output = string.Empty;

            if (arguments.Contains("--version"))
            {
                output = VersionOutput;
            }

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
                StandardOutput = output,
                StandardError = string.Empty,
            };
        }
    }
}
