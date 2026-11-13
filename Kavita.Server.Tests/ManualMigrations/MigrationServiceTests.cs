using System;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Database;
using Kavita.Database.Tests;
using Kavita.Models.Entities.Enums;
using Kavita.Server.ManualMigrations;
using Kavita.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Server.Tests.ManualMigrations;

public class MigrationServiceTests(ITestOutputHelper testOutputHelper) : AbstractDbTest(testOutputHelper)
{
    private static readonly string MigrationDirectory = ConfigDirectory + "migrations/";
    private static readonly string DatabaseFile = ConfigDirectory + "kavita.db";

    private readonly MockFileSystem _fileSystem = CreateFileSystem();
    private readonly IBackupService _backupService = Substitute.For<IBackupService>();

    [Fact]
    public async Task Restore_NoSnapshotThisRun_LeavesDbAlone()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        _fileSystem.AddFile(DatabaseFile, new MockFileData("current"));
        _fileSystem.AddFile(MigrationDirectory + "v0.9.1.22/kavita.db", new MockFileData("old snapshot"));

        await CreateService(unitOfWork, context).RestoreDatabase(new Exception("seed failed"));

        Assert.Equal("current", _fileSystem.File.ReadAllText(DatabaseFile));
    }

    [Fact]
    public async Task Snapshot_NoInstallVersion_UnderMigrationDirectory()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        await context.ServerSetting.Where(s => s.Key == ServerSettingKey.InstallVersion).ExecuteDeleteAsync();

        await CreateService(unitOfWork, context).CreateSnapshot();

        await _backupService.Received(1).SnapshotDatabase(
            Arg.Is<string>(p => p.Replace('\\', '/').StartsWith(MigrationDirectory + "unknown_") && p.EndsWith("kavita.db")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Snapshot_VersionChangedNoEfPending_NoSnapshot()
    {
        var (unitOfWork, context, _) = await CreateDatabase();
        await MarkAllMigrationsApplied(context);
        await context.ServerSetting.Where(s => s.Key == ServerSettingKey.InstallVersion)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Value, "0.1.0"));

        await CreateService(unitOfWork, context).CreateSnapshot();

        await _backupService.DidNotReceive().SnapshotDatabase(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private MigrationService CreateService(IUnitOfWork unitOfWork, DataContext context)
    {
        var directoryService = new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), _fileSystem);
        return new MigrationService(unitOfWork, context, Substitute.For<IVersionUpdaterService>(), directoryService,
            _backupService, Substitute.For<ILogger<MigrationService>>());
    }

    private static async Task MarkAllMigrationsApplied(DataContext context)
    {
        var history = context.GetService<IHistoryRepository>();
        await context.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
        var productVersion = typeof(DbContext).Assembly.GetName().Version!.ToString();
        foreach (var id in context.Database.GetMigrations())
        {
            await context.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(id, productVersion)));
        }
    }
}
