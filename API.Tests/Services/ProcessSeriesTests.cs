using System.IO;
using API.Data;
using API.Data.Metadata;
using API.Entities;
using API.Entities.Enums;
using API.Helpers;
using API.Helpers.Builders;
using API.Services;
using API.Services.Tasks.Metadata;
using API.Services.Tasks.Scanner;
using API.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace API.Tests.Services;

public class ProcessSeriesTests
{
    // TODO: Implement

    #region UpdateSeriesMetadata



    #endregion

    #region UpdateVolumes



    #endregion

    #region UpdateChapters



    #endregion

    #region AddOrUpdateFileForChapter



    #endregion

    #region UpdateChapterFromComicInfo

    // public void UpdateChapterFromComicInfo_()
    // {
    //     // TODO: Do this
    //     var file = Path.Join(Directory.GetCurrentDirectory(), "../../../Services/Test Data/ScannerService/Library/Manga/Hajime no Ippo/Hajime no Ippo Chapter 1.cbz");
    //     // Chapter and ComicInfo
    //     var chapter = new ChapterBuilder("1")
    //         .WithId(0)
    //         .WithFile(new MangaFileBuilder(file, MangaFormat.Archive).Build())
    //         .Build();
    //
    //     var ps = new ProcessSeries(Substitute.For<IUnitOfWork>(), Substitute.For<ILogger<ProcessSeries>>(),
    //         Substitute.For<IEventHub>(), Substitute.For<IDirectoryService>()
    //         , Substitute.For<ICacheHelper>(), Substitute.For<IReadingItemService>(), Substitute.For<IFileService>(),
    //         Substitute.For<IMetadataService>(),
    //         Substitute.For<IWordCountAnalyzerService>(),
    //         Substitute.For<ICollectionTagService>(), Substitute.For<IReadingListService>());
    //
    //     ps.UpdateChapterFromComicInfo(chapter, new ComicInfo()
    //     {
    //
    //     });
    // }

    #endregion
}
