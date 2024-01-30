using System.Globalization;
using System.IO;
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

        if (!File.Exists(importFile))
        {
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
            var user = await unitOfWork.UserRepository.GetUserByIdAsync(appUserId, AppUserIncludes.WantToRead);
            if (user == null) continue;
            user.WantToRead.Add(new AppUserWantToRead()
            {
                SeriesId = csvReader.GetField<int>("Id")
            });
        }

        await unitOfWork.CommitAsync();
    }
}
