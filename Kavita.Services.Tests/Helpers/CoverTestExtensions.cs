using System.Threading;
using Kavita.Models.Entities;
using Kavita.Models.Images;
using Kavita.Services.Builders;

namespace Kavita.Services.Tests.Helpers;

public static class CoverTestExtensions
{
    private static int _nextRowId;

    /// <summary>
    /// Links the chapter to an unsaved cover row whose file name is <paramref name="label"/>, so a test can tell which
    /// entity a cover lookup picked
    /// </summary>
    public static ChapterBuilder WithCover(this ChapterBuilder builder, string label)
    {
        var row = new ImageFile { Id = Interlocked.Increment(ref _nextRowId), Kind = ImageKind.ChapterCover, FileName = label };
        var chapter = builder.Build();
        chapter.CoverImageFile = row;
        chapter.CoverImageFileId = row.Id;
        return builder;
    }

    public static void ShareCover(this Volume volume, Chapter? source)
    {
        volume.CoverImageFile = source?.CoverImageFile;
        volume.CoverImageFileId = source?.CoverImageFileId;
    }
}
