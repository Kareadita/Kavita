using System;
using System.Linq;
using System.Threading.Tasks;
using API.Data.Misc;
using API.Entities;
using API.Entities.Enums;
using API.Entities.History;
using Kavita.Common.EnvironmentInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Data.ManualMigrations;

/// <summary>
/// Introduced in v0.7.11 with the removal of .Kavitaignore files
/// </summary>
public class MigrateLibrariesToHaveAllFileTypes : ManualMigration
{
    protected override string MigrationName => nameof(MigrateLibrariesToHaveAllFileTypes);
    protected override async Task ExecuteAsync(DataContext context, ILogger<Program> logger)
    {
        var allLibs = await context.Library
            .Include(l => l.LibraryFileTypes)
            .Where(library => library.LibraryFileTypes.Count == 0)
            .ToListAsync();

        foreach (var library in allLibs)
        {
            switch (library.Type)
            {
                case LibraryType.Manga:
                case LibraryType.Comic:
                    library.LibraryFileTypes.Add(new LibraryFileTypeGroup()
                    {
                        FileTypeGroup = FileTypeGroup.Archive
                    });
                    library.LibraryFileTypes.Add(new LibraryFileTypeGroup()
                    {
                        FileTypeGroup = FileTypeGroup.Epub
                    });
                    library.LibraryFileTypes.Add(new LibraryFileTypeGroup()
                    {
                        FileTypeGroup = FileTypeGroup.Images
                    });
                    library.LibraryFileTypes.Add(new LibraryFileTypeGroup()
                    {
                        FileTypeGroup = FileTypeGroup.Pdf
                    });
                    break;
                case LibraryType.Book:
                    library.LibraryFileTypes.Add(new LibraryFileTypeGroup()
                    {
                        FileTypeGroup = FileTypeGroup.Pdf
                    });
                    library.LibraryFileTypes.Add(new LibraryFileTypeGroup()
                    {
                        FileTypeGroup = FileTypeGroup.Epub
                    });
                    break;
                case LibraryType.Image:
                    library.LibraryFileTypes.Add(new LibraryFileTypeGroup()
                    {
                        FileTypeGroup = FileTypeGroup.Images
                    });
                    break;
            }
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync();
        }
    }
}
