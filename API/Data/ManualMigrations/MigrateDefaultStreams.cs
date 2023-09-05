using System.Collections.Generic;
using System.Threading.Tasks;
using API.Data.Repositories;
using API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// Added in v0.7.8.2
/// </summary>
public static class MigrateDefaultStreams
{

    public static async Task Migrate(IUnitOfWork unitOfWork, DataContext context, ILogger<Program> logger)
    {
        //if (await context.AppUserDashboardStream.AnyAsync()) return;

        logger.LogCritical("Running MigrateDefaultStreams migration");

        var allUsers = await unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.DashboardStreams);
        foreach (var user in allUsers)
        {
            if (user.DashboardStreams.Count != 0) continue;
            user.DashboardStreams ??= new List<AppUserDashboardStream>();
            foreach (var defaultStream in Seed.DefaultStreams)
            {
                var newStream = new AppUserDashboardStream
                {
                    Name = defaultStream.Name,
                    IsProvided = defaultStream.IsProvided,
                    Order = defaultStream.Order,
                    StreamType = defaultStream.StreamType,
                };

                user.DashboardStreams.Add(newStream);
            }
            unitOfWork.UserRepository.Update(user);
            await unitOfWork.CommitAsync();
        }



        logger.LogInformation("MigrateDefaultStreams migration complete");
    }
}
