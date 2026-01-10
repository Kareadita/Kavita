using System;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Data.AutoMapper;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using AutoMapper;
using Hangfire;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit.Abstractions;

namespace API.Tests;
#nullable enable

public abstract class AbstractDbTest(ITestOutputHelper testOutputHelper): AbstractFsTest, IAsyncDisposable
{

    private SqliteConnection? _connection;
    private DataContext? _context;

    protected async Task<(IUnitOfWork, DataContext, IMapper)> CreateDatabase()
    {
        // Dispose any previous connection if CreateDatabase is called multiple times
        if (_connection != null)
        {
            await _context!.DisposeAsync();
            await _connection.DisposeAsync();
        }
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var contextOptions = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        _context = new DataContext(contextOptions);

        await _context.Database.EnsureCreatedAsync();

        await SeedDb(_context);


        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddMaps(typeof(AutoMapperProfiles).Assembly);
        });
        var mapper = config.CreateMapper();

        GlobalConfiguration.Configuration.UseInMemoryStorage();
        var unitOfWork = new UnitOfWork(_context, mapper, null);

        return (unitOfWork, _context, mapper);
    }

    private async Task<bool> SeedDb(DataContext context)
    {
        try
        {
            var filesystem = CreateFileSystem();
            await Seed.SeedSettings(context, new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem));

            var setting = await context.ServerSetting.Where(s => s.Key == ServerSettingKey.CacheDirectory).SingleAsync();
            setting.Value = CacheDirectory;

            setting = await context.ServerSetting.Where(s => s.Key == ServerSettingKey.BackupDirectory).SingleAsync();
            setting.Value = BackupDirectory;

            setting = await context.ServerSetting.Where(s => s.Key == ServerSettingKey.BookmarkDirectory).SingleAsync();
            setting.Value = BookmarkDirectory;

            setting = await context.ServerSetting.Where(s => s.Key == ServerSettingKey.TotalLogs).SingleAsync();
            setting.Value = "10";

            context.ServerSetting.Update(setting);


            context.Library.Add(new LibraryBuilder("Manga")
                .WithAllowMetadataMatching(true)
                .WithFolderPath(new FolderPathBuilder(DataDirectory).Build())
                .Build());

            await context.SaveChangesAsync();

            await Seed.SeedMetadataSettings(context);

            return true;
        }
        catch (Exception ex)
        {
            testOutputHelper.WriteLine($"[SeedDb] Error: {ex.Message} \n{ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Add a role to an existing User. Commits.
    /// </summary>
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
