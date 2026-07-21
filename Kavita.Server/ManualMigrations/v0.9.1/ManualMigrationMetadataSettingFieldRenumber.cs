using System.Threading.Tasks;
using Kavita.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1;

/// <summary>
/// v0.9.0.15 - I updated the Overrides enum to give more spacing for fields
/// </summary>
public class ManualMigrationMetadataSettingFieldRenumber : ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrationMetadataSettingFieldRenumber);

    private static readonly (string Table, string Column)[] Targets =
    {
        ("MetadataSettings", "Overrides"),
        ("Chapter", "KPlusOverrides"),
        ("Volume", "KPlusOverrides"),
        ("SeriesMetadata", "KPlusOverrides"),
    };

    private static readonly (string From, string To)[] Remaps =
    {
        ("10", "20"), ("11", "21"), ("12", "22"), ("13", "23"),
        ("14", "24"), ("16", "25"), ("15", "40"),
    };

    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        foreach (var (table, column) in Targets)
        {
            var expr = column;
            foreach (var (from, to) in Remaps)
            {
                expr = $"REPLACE({expr}, '{from}', '{to}')";
            }

            var sql = $"UPDATE {table} SET {column} = {expr} WHERE {column} IS NOT NULL;";
            var rows = await context.Database.ExecuteSqlRawAsync(sql);
            logger.LogDebug("Renumbered MetadataSettingField values in {Table}.{Column} ({Rows} rows)", table, column, rows);
        }
    }
}
