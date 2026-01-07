using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Misc;
using API.Data.Repositories;
using API.Entities;
using API.Services;
using CsvHelper;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.7.13.12/v0.7.14 - Want to read is imported from a csv
/// </summary>
public class MigrateWantToReadImport : ManualMigration
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDirectoryService _directoryService;

    protected override string MigrationName => "MigrateWantToReadImport";

    public MigrateWantToReadImport(IUnitOfWork unitOfWork, IDirectoryService directoryService)
    {
        _unitOfWork = unitOfWork;
        _directoryService = directoryService;
    }

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var importFile = Path.Join(_directoryService.ConfigDirectory, "want-to-read-migration.csv");
        var outputFile = Path.Join(_directoryService.ConfigDirectory, "imported-want-to-read-migration.csv");

        if (!File.Exists(importFile))
        {
            logger.LogInformation("No want-to-read import file found, skipping");
            return;
        }

        if (File.Exists(outputFile))
        {
            logger.LogInformation("Want-to-read already imported (output file exists), skipping");
            return;
        }

        var importedCount = 0;

        using var reader = new StreamReader(importFile);
        using var csvReader = new CsvReader(reader, CultureInfo.InvariantCulture);

        await csvReader.ReadAsync();
        csvReader.ReadHeader();

        while (await csvReader.ReadAsync())
        {
            var appUserId = csvReader.GetField<int>("AppUserId");
            var seriesId = csvReader.GetField<int>("Id");

            var user = await _unitOfWork.UserRepository.GetUserByIdAsync(appUserId, AppUserIncludes.WantToRead);
            if (user == null || user.WantToRead.Any(w => w.SeriesId == seriesId))
            {
                continue;
            }

            user.WantToRead.Add(new AppUserWantToRead
            {
                SeriesId = seriesId
            });
            importedCount++;
        }

        await _unitOfWork.CommitAsync();

        // Copy to output file to mark as processed
        File.Copy(importFile, outputFile);

        logger.LogInformation("Imported {Count} want-to-read entries", importedCount);
    }
}
