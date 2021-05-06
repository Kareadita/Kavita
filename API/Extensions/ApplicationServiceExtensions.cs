using API.Data;
using API.Helpers;
using API.Interfaces;
using API.Interfaces.Services;
using API.Services;
using API.Services.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace API.Extensions
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration config)
        {
            services.AddAutoMapper(typeof(AutoMapperProfiles).Assembly);
            services.AddScoped<ITaskScheduler, TaskScheduler>();
            services.AddScoped<IDirectoryService, DirectoryService>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<ICacheService, CacheService>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IScannerService, ScannerService>();
            services.AddScoped<IArchiveService, ArchiveService>();
            services.AddScoped<IMetadataService, MetadataService>();
            services.AddScoped<IBackupService, BackupService>();
            services.AddScoped<ICleanupService, CleanupService>();
            services.AddScoped<IBookService, BookService>();
            

            services.AddDbContext<DataContext>(options =>
            {
                options.UseSqlite(config.GetConnectionString("DefaultConnection"));
            });

            services.AddLogging(loggingBuilder =>
            {
                var loggingSection = config.GetSection("Logging");
                loggingBuilder.AddFile(loggingSection);
            });
            
            return services;
        }
        
        public static IServiceCollection AddStartupTask<T>(this IServiceCollection services)
            where T : class, IStartupTask
            => services.AddTransient<IStartupTask, T>();
    }
}