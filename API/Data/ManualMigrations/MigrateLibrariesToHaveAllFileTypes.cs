using System;
using System.Linq;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.Enums;
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
        logger.LogCritical("Running MigrateLibrariesToHaveAllFileTypes migration - Please be patient, this may take some time. This is not an error");
        var allLibs = await dataContext.Library.Include(l => l.LibraryFileTypes).ToListAsync();
        foreach (var library in allLibs.Where(library => library.LibraryFileTypes.Count == 0))
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
                    throw new ArgumentOutOfRangeException();
            }
        }

        await dataContext.SaveChangesAsync();
        logger.LogCritical("Running MigrateLibrariesToHaveAllFileTypes migration - Completed. This is not an error");
    }
}
