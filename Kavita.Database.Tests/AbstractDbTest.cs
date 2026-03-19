using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Hangfire;
using Kavita.API.Database;
using Kavita.API.Services;
using Kavita.Models.AutoMapper;
using Kavita.Models.Builders;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit.Abstractions;

namespace Kavita.Database.Tests;

public abstract class AbstractDbTest(ITestOutputHelper testOutputHelper): AbstractFsTest, IAsyncDisposable
{

    private SqliteConnection? _connection;
    private DataContext? _context;

    protected async Task<(IUnitOfWork, DataContext, IMapper)> CreateDatabase()
    {

        GlobalConfiguration.Configuration.UseInMemoryStorage();

        // Dispose any previous connection if CreateDatabase is called multiple times
        if (_connection != null)
        {
            await _context!.DisposeAsync();
            await _connection.DisposeAsync();
        }
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var contextOptions = ((DbContextOptionsBuilder)new DbContextOptionsBuilder<DataContext>()
                .UseSqlite(_connection)).EnableSensitiveDataLogging()
            .Options;

        _context = new DataContext(contextOptions);

        await _context.Database.EnsureCreatedAsync();

        await SeedDb(_context);


        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddMaps(typeof(AutoMapperProfiles).Assembly);
        });
        var mapper = config.CreateMapper();

        var unitOfWork = new UnitOfWork(_context, mapper, null!);

        _context.ChangeTracker.Clear();

        return (unitOfWork, _context, mapper);
    }

    private async Task SeedDb(DataContext context)
    {
        try
        {
            var directoryService = Substitute.For<IDirectoryService>();
            directoryService.BackupDirectory.Returns(BackupDirectory);

            await Seed.SeedSettings(context, directoryService);
            context.ChangeTracker.Clear();

            var cacheSetting = await context.ServerSetting.Where(s => s.Key == ServerSettingKey.CacheDirectory).SingleAsync();
            cacheSetting.Value = CacheDirectory;

            var backupSetting = await context.ServerSetting.Where(s => s.Key == ServerSettingKey.BackupDirectory).SingleAsync();
            backupSetting.Value = BackupDirectory;

            var bookmarkSetting = await context.ServerSetting.Where(s => s.Key == ServerSettingKey.BookmarkDirectory).SingleAsync();
            bookmarkSetting.Value = BookmarkDirectory;

            var logSetting = await context.ServerSetting.Where(s => s.Key == ServerSettingKey.TotalLogs).SingleAsync();
            logSetting.Value = "10";

            await context.SaveChangesAsync();


            context.Library.Add(new LibraryBuilder("Manga")
                .WithAllowMetadataMatching(true)
                .WithFolderPath(new FolderPathBuilder(DataDirectory).Build())
                .Build());

            await context.SaveChangesAsync();

            await Seed.SeedMetadataSettings(context);
        }
        catch (Exception ex)
        {
            testOutputHelper.WriteLine($"[SeedDb] Error: {ex.Message} \n{ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Add a role to an existing User. Commits.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="userId"></param>
    /// <param name="roleName"></param>
    protected static async Task AddUserWithRole(DataContext context, int userId, string roleName)
    {
        var role = new AppRole { Id = userId, Name = roleName, NormalizedName = roleName.ToUpper() };

        await context.Roles.AddAsync(role);
        await context.UserRoles.AddAsync(new AppUserRole { UserId = userId, RoleId = userId });

        await context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_context != null)
        {
            await _context.DisposeAsync();
        }

        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

}
