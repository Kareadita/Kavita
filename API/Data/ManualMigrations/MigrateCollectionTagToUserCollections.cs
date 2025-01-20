using System;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Repositories;
using API.Entities;
using API.Entities.Enums;
using API.Entities.History;
using API.Extensions.QueryExtensions;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.8.0 refactored User Collections
/// </summary>
public static class MigrateCollectionTagToUserCollections
{
    public static async Task Migrate(DataContext dataContext, IUnitOfWork unitOfWork, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateCollectionTagToUserCollections") ||
            !await dataContext.AppUser.AnyAsync())
        {
            return;
        }

        logger.LogCritical(
            "Running MigrateCollectionTagToUserCollections migration - Please be patient, this may take some time. This is not an error");

        // Find the first user that is an admin
        var defaultAdmin = await unitOfWork.UserRepository.GetDefaultAdminUser(AppUserIncludes.Collections);
        if (defaultAdmin == null)
        {
            await CompleteMigration(dataContext, logger);
            return;
        }

        // For all collectionTags, move them over to said user
        var existingCollections = await dataContext.CollectionTag
            .OrderBy(c => c.NormalizedTitle)
            .Includes(CollectionTagIncludes.SeriesMetadataWithSeries)
            .ToListAsync();
        foreach (var existingCollectionTag in existingCollections)
        {
            var collection = new AppUserCollection()
            {
                Title = existingCollectionTag.Title,
                NormalizedTitle = existingCollectionTag.Title.Normalize(),
                CoverImage = existingCollectionTag.CoverImage,
                CoverImageLocked = existingCollectionTag.CoverImageLocked,
                Promoted = existingCollectionTag.Promoted,
                AgeRating = AgeRating.Unknown,
                Summary = existingCollectionTag.Summary,
                Items = existingCollectionTag.SeriesMetadatas.Select(s => s.Series).ToList()
            };

            collection.AgeRating = await unitOfWork.SeriesRepository.GetMaxAgeRatingFromSeriesAsync(collection.Items.Select(s => s.Id));
            defaultAdmin.Collections.Add(collection);
        }
        unitOfWork.UserRepository.Update(defaultAdmin);

        await unitOfWork.CommitAsync();

        await CompleteMigration(dataContext, logger);
    }

    private static async Task CompleteMigration(DataContext dataContext, ILogger<Program> logger)
    {
        dataContext.ManualMigrationHistory.Add(new ManualMigrationHistory()
        {
            Name = "MigrateCollectionTagToUserCollections",
            ProductVersion = BuildInfo.Version.ToString(),
            RanAt = DateTime.UtcNow
        });

        await dataContext.SaveChangesAsync();

        logger.LogCritical(
            "Running MigrateCollectionTagToUserCollections migration - Completed. This is not an error");
    }
}
