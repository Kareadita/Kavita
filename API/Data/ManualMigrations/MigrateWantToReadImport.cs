using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Repositories;
using API.Entities;
using API.Services;
using CsvHelper;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.7.13.12/v0.7.14 - Want to read is imported from a csv
/// </summary>
public static class MigrateWantToReadImport
{
    public static async Task Migrate(IUnitOfWork unitOfWork, IDirectoryService directoryService, ILogger<Program> logger)
    {
        var importFile = Path.Join(directoryService.ConfigDirectory, "want-to-read-migration.csv");
        var outputFile = Path.Join(directoryService.ConfigDirectory, "imported-want-to-read-migration.csv");

        logger.LogCritical(
            "Running MigrateWantToReadImport migration - Please be patient, this may take some time. This is not an error");

        if (!File.Exists(importFile) || File.Exists(outputFile))
        {
            logger.LogCritical(
                "Running MigrateWantToReadImport migration - Completed. This is not an error");
            return;
        }

        using var reader = new StreamReader(importFile);
        using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);
        // Read the records from the CSV file
        await csvReader.ReadAsync();
        csvReader.ReadHeader(); // Skip the header row

        while (await csvReader.ReadAsync())
        {
            // Read the values of AppUserId and Id columns
            var appUserId = csvReader.GetField<int>("AppUserId");
            var seriesId = csvReader.GetField<int>("Id");
            var user = await unitOfWork.UserRepository.GetUserByIdAsync(appUserId, AppUserIncludes.WantToRead);
            if (user == null || user.WantToRead.Any(w => w.SeriesId == seriesId)) continue;

            user.WantToRead.Add(new AppUserWantToRead()
            {
                SeriesId = seriesId
            });
        }

        await unitOfWork.CommitAsync();
        reader.Close();

        File.WriteAllLines(outputFile, await File.ReadAllLinesAsync(importFile));
        logger.LogCritical(
            "Running MigrateWantToReadImport migration - Completed. This is not an error");
    }
}
