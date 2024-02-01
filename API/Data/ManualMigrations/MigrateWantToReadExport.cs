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
        logger.LogCritical(
            "Running MigrateWantToReadExport migration - Please be patient, this may take some time. This is not an error");

        var columnExists = false;
        await using var command = dataContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA table_info('Series')";

        await dataContext.Database.OpenConnectionAsync();
        await using var result = await command.ExecuteReaderAsync();
        while (await result.ReadAsync())
        {
            var columnName = result["name"].ToString();
            if (columnName != "AppUserId") continue;

            logger.LogInformation("Column 'AppUserId' exists in the 'Series' table. Running migration...");
            // Your migration logic here
            columnExists = true;
            break;
        }

        await result.CloseAsync();

        if (!columnExists)
        {
            logger.LogCritical(
                "Running MigrateWantToReadExport migration - Completed. This is not an error");
            return;
        }

        await using var command2 = dataContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "Select AppUserId, Id from Series WHERE AppUserId IS NOT NULL ORDER BY AppUserId;";

        await dataContext.Database.OpenConnectionAsync();
        await using var result2 = await command.ExecuteReaderAsync();

        await using var writer = new StreamWriter(Path.Join(directoryService.ConfigDirectory, "want-to-read-migration.csv"));
        await using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Write header
        csvWriter.WriteField("AppUserId");
        csvWriter.WriteField("Id");
        await csvWriter.NextRecordAsync();

        // Write data
        while (await result2.ReadAsync())
        {
            var appUserId = result2["AppUserId"].ToString();
            var id = result2["Id"].ToString();

            csvWriter.WriteField(appUserId);
            csvWriter.WriteField(id);
            await csvWriter.NextRecordAsync();
        }


        await result2.CloseAsync();
        writer.Close();

        logger.LogCritical(
            "Running MigrateWantToReadExport migration - Completed. This is not an error");
    }
}
