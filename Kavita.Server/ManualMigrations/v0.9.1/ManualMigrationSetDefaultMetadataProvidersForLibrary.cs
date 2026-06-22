using System;
using System.Threading.Tasks;
using Kavita.Database;
using Kavita.Models.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._1;

public class ManualMigrationSetDefaultMetadataProvidersForLibrary: ManualMigration
{
    protected override string MigrationName => nameof(ManualMigrationSetDefaultMetadataProvidersForLibrary);
    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var libraries = await context.Library.ToListAsync();

        foreach (var library in libraries)
        {
            library.MetadataProvider = library.Type switch
            {
                LibraryType.Manga => MetadataProvider.Mangabaka,
                LibraryType.Comic => MetadataProvider.ComicBookRoundup,
                LibraryType.Book => MetadataProvider.Hardcover,
                LibraryType.Image => MetadataProvider.Mangabaka,
                LibraryType.LightNovel => MetadataProvider.Mangabaka,
                LibraryType.ComicVine => MetadataProvider.ComicBookRoundup,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        await context.SaveChangesAsync();
    }
}
