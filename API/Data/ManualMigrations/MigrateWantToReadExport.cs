using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using API.Services;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;


/// <summary>
/// v0.7.13.12/v0.7.14 - Want to read is extracted and saved in a csv
/// </summary>
/// <remarks>This must run BEFORE any DB migrations</remarks>
public static class MigrateWantToReadExport
{
    public static async Task Migrate(DataContext dataContext, IDirectoryService directoryService, ILogger<Program> logger)
    {
        try
        {

            if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateWantToReadExport"))
            {
                return;
            }

            var importFile = Path.Join(directoryService.ConfigDirectory, "want-to-read-migration.csv");
            if (File.Exists(importFile))
            {
                logger.LogCritical(
                    "Running MigrateWantToReadExport migration - Completed. This is not an error");
                return;
            }

            logger.LogCritical(
                "Running MigrateWantToReadExport migration - Please be patient, this may take some time. This is not an error");

            await using var command = dataContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "Select AppUserId, Id from Series WHERE AppUserId IS NOT NULL ORDER BY AppUserId;";

            await dataContext.Database.OpenConnectionAsync();
            await using var result = await command.ExecuteReaderAsync();

            await using var writer =
                new StreamWriter(Path.Join(directoryService.ConfigDirectory, "want-to-read-migration.csv"));
            await using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

            // Write header
            csvWriter.WriteField("AppUserId");
            csvWriter.WriteField("Id");
            await csvWriter.NextRecordAsync();

            // Write data
            while (await result.ReadAsync())
            {
                var appUserId = result["AppUserId"].ToString();
                var id = result["Id"].ToString();

                csvWriter.WriteField(appUserId);
                csvWriter.WriteField(id);
                await csvWriter.NextRecordAsync();
            }


            try
            {
                await dataContext.Database.CloseConnectionAsync();
                writer.Close();
            }
            catch (Exception)
            {
                /* Swallow */
            }

            logger.LogCritical(
                "Running MigrateWantToReadExport migration - Completed. This is not an error");
        }
        catch (Exception ex)
        {
            // On new installs, the db isn't setup yet, so this has nothing to do
        }
    }
}
