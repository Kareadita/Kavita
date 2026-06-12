using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1;

public class ManualMigrationMetadataProvider: ManualMigration
{
    protected override string MigrationName { get; } = nameof(ManualMigrationMetadataProvider);
    protected override Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        return context.ExternalSeriesMetadata
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Provider, MetadataProvider.Mangabaka));
    }
}
