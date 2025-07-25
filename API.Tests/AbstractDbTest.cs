using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using API.Data;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using AutoMapper;
using Hangfire;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace API.Tests;

public abstract class AbstractDbTest : AbstractFsTest , IDisposable
{
    protected readonly DataContext Context;
    protected readonly IUnitOfWork UnitOfWork;
    protected readonly IMapper Mapper;
    private readonly DbConnection _connection;
    private bool _disposed;

    protected AbstractDbTest()
    {
        var contextOptions = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(CreateInMemoryDatabase())
            .EnableSensitiveDataLogging()
            .Options;

        _connection = RelationalOptionsExtension.Extract(contextOptions).Connection;

        Context = new DataContext(contextOptions);

        Context.Database.EnsureCreated(); // Ensure DB schema is created

        Task.Run(SeedDb).GetAwaiter().GetResult();

        var config = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfiles>());
        Mapper = config.CreateMapper();

        GlobalConfiguration.Configuration.UseInMemoryStorage();
        UnitOfWork = new UnitOfWork(Context, Mapper, null);
    }

    private static DbConnection CreateInMemoryDatabase()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        return connection;
    }

    private async Task<bool> SeedDb()
    {
        try
        {
            await Context.Database.EnsureCreatedAsync();
            var filesystem = CreateFileSystem();

            await Seed.SeedSettings(Context, new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), filesystem));

            var setting = await Context.ServerSetting.Where(s => s.Key == ServerSettingKey.CacheDirectory).SingleAsync();
            setting.Value = CacheDirectory;

            setting = await Context.ServerSetting.Where(s => s.Key == ServerSettingKey.BackupDirectory).SingleAsync();
            setting.Value = BackupDirectory;

            setting = await Context.ServerSetting.Where(s => s.Key == ServerSettingKey.BookmarkDirectory).SingleAsync();
            setting.Value = BookmarkDirectory;

            setting = await Context.ServerSetting.Where(s => s.Key == ServerSettingKey.TotalLogs).SingleAsync();
            setting.Value = "10";

            Context.ServerSetting.Update(setting);


            Context.Library.Add(new LibraryBuilder("Manga")
                .WithAllowMetadataMatching(true)
                .WithFolderPath(new FolderPathBuilder(DataDirectory).Build())
                .Build());

            await Context.SaveChangesAsync();

            await Seed.SeedMetadataSettings(Context);

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SeedDb] Error: {ex.Message}");
            return false;
        }
    }

    protected abstract Task ResetDb();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            Context?.Dispose();
            _connection?.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// Add a role to an existing User. Commits.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="roleName"></param>
    protected async Task AddUserWithRole(int userId, string roleName)
    {
        var role = new AppRole { Id = userId, Name = roleName, NormalizedName = roleName.ToUpper() };

        await Context.Roles.AddAsync(role);
        await Context.UserRoles.AddAsync(new AppUserRole { UserId = userId, RoleId = userId });

        await Context.SaveChangesAsync();
    }
}
