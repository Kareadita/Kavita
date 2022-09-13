using System;
using System.Linq;
using System.Threading.Tasks;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data;

/// <summary>
/// v0.6.0 introduced a change in how Normalization works and hence every normalized field needs to be re-calculated
/// </summary>
public static class MigrateNormalizedEverything
{
    public static async Task Migrate(IUnitOfWork unitOfWork, DataContext dataContext, ILogger<Program> logger)
    {
        // if current version is > 0.5.6.3, then we can exit and not perform
        var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync();
        if (Version.Parse(settings.InstallVersion) > new Version(0, 5, 6, 3))
        {
            return;
        }
        logger.LogCritical("Running MigrateNormalizedEverything migration. Please be patient, this may take some time depending on the size of your library. Do not abort, this can break your Database");

        logger.LogInformation("Updating Normalization on Series...");
        foreach (var series in await dataContext.Series.ToListAsync())
        {
            series.NormalizedLocalizedName = Services.Tasks.Scanner.Parser.Parser.Normalize(series.LocalizedName ?? string.Empty);
            series.NormalizedName = Services.Tasks.Scanner.Parser.Parser.Normalize(series.Name ?? string.Empty);
            logger.LogInformation("Updated Series: {SeriesName}", series.Name);
            unitOfWork.SeriesRepository.Update(series);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Normalization on Series...Done");

        // Genres
        logger.LogInformation("Updating Normalization on Genres...");
        foreach (var genre in await dataContext.Genre.ToListAsync())
        {
            genre.NormalizedTitle = Services.Tasks.Scanner.Parser.Parser.Normalize(genre.Title ?? string.Empty);
            logger.LogInformation("Updated Genre: {Genre}", genre.Title);
            unitOfWork.GenreRepository.Attach(genre);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Normalization on Genres...Done");

        // Tags
        logger.LogInformation("Updating Normalization on Tags...");
        foreach (var tag in await dataContext.Tag.ToListAsync())
        {
            tag.NormalizedTitle = Services.Tasks.Scanner.Parser.Parser.Normalize(tag.Title ?? string.Empty);
            logger.LogInformation("Updated Tag: {Tag}", tag.Title);
            unitOfWork.TagRepository.Attach(tag);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Normalization on Tags...Done");

        // People
        logger.LogInformation("Updating Normalization on People...");
        foreach (var person in await dataContext.Person.ToListAsync())
        {
            person.NormalizedName = Services.Tasks.Scanner.Parser.Parser.Normalize(person.Name ?? string.Empty);
            logger.LogInformation("Updated Person: {Person}", person.Name);
            unitOfWork.PersonRepository.Attach(person);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Normalization on People...Done");

        // Collections
        logger.LogInformation("Updating Normalization on Collections...");
        foreach (var collection in await dataContext.CollectionTag.ToListAsync())
        {
            collection.NormalizedTitle = Services.Tasks.Scanner.Parser.Parser.Normalize(collection.Title ?? string.Empty);
            logger.LogInformation("Updated Collection: {Collection}", collection.Title);
            unitOfWork.CollectionTagRepository.Update(collection);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Normalization on Collections...Done");

        // Reading Lists
        logger.LogInformation("Updating Normalization on Reading Lists...");
        foreach (var readingList in await dataContext.ReadingList.ToListAsync())
        {
            readingList.NormalizedTitle = Services.Tasks.Scanner.Parser.Parser.Normalize(readingList.Title ?? string.Empty);
            logger.LogInformation("Updated Reading List: {ReadingList}", readingList.Title);
            unitOfWork.ReadingListRepository.Update(readingList);
        }

        if (unitOfWork.HasChanges())
        {
            await unitOfWork.CommitAsync();
        }
        logger.LogInformation("Updating Normalization on Reading Lists...Done");


        logger.LogInformation("MigrateNormalizedEverything migration finished");

    }

}
