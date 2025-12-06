using System;
using System.Linq;
using System.Threading.Tasks;
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
public static class MigrateLibrariesToHaveAllFileTypes
{
    public static async Task Migrate(IUnitOfWork unitOfWork, DataContext dataContext, ILogger<Program> logger)
    {
        if (await dataContext.ManualMigrationHistory.AnyAsync(m => m.Name == "MigrateLibrariesToHaveAllFileTypes"))
        {
            await SaveManualHistoryItem(dataContext);
            return;
        }

        logger.LogCritical("Running MigrateLibrariesToHaveAllFileTypes migration - Please be patient, this may take some time. This is not an error");

        var allLibs = await dataContext.Library
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
                default:
                    break;
            }
        }

        if (unitOfWork.HasChanges())
        {
            await dataContext.SaveChangesAsync();
        }

        await SaveManualHistoryItem(dataContext);

        logger.LogCritical("Running MigrateLibrariesToHaveAllFileTypes migration - Completed. This is not an error");
    }

    private static async Task SaveManualHistoryItem(DataContext context)
    {
        await context.ManualMigrationHistory.AddAsync(new ManualMigrationHistory()
        {
            Name = "MigrateLibrariesToHaveAllFileTypes",
        });
        await context.SaveChangesAsync();
    }
}
