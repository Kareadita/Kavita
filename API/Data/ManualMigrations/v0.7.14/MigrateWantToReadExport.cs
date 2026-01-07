using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using API.Data.Misc;
using API.Services;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// v0.7.13.12/v0.7.14 - Want to read is extracted and saved in a csv
/// </summary>
/// <remarks>This must run BEFORE any DB migrations</remarks>
public class MigrateWantToReadExport : ManualMigration
{
    private readonly IDirectoryService _directoryService;

    protected override string MigrationName => "MigrateWantToReadExport";

    public MigrateWantToReadExport(IDirectoryService directoryService)
    {
        _directoryService = directoryService;
    }

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var importFile = Path.Join(_directoryService.ConfigDirectory, "want-to-read-migration.csv");
        if (File.Exists(importFile))
        {
            logger.LogInformation("Want-to-read migration file already exists, skipping export");
            return;
        }

        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT AppUserId, Id FROM Series WHERE AppUserId IS NOT NULL ORDER BY AppUserId;";

        await context.Database.OpenConnectionAsync();
        try
        {
            await using var result = await command.ExecuteReaderAsync();

            await using var writer = new StreamWriter(importFile);
            await using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

            csvWriter.WriteField("AppUserId");
            csvWriter.WriteField("Id");
            await csvWriter.NextRecordAsync();

            while (await result.ReadAsync())
            {
                csvWriter.WriteField(result["AppUserId"].ToString());
                csvWriter.WriteField(result["Id"].ToString());
                await csvWriter.NextRecordAsync();
            }
        }
        catch (Exception ex)
        {
            // This migration might run on versions of Kavita with schema changes, swallow so the Migration counts as ran
            if (!ex.Message.Contains("no such column"))
            {
                logger.LogError(ex, "An error occured while importing want to read file");
            }

        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
