using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.Data;
using API.Data.Repositories;
using API.DTOs.Reader;
using API.DTOs.Settings;
using API.Entities;
using API.Helpers.Builders;
using API.Services;
using API.SignalR;
using AutoMapper;
using Kavita.Common;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace API.Tests.Services;

public class AnnotationServiceTests(ITestOutputHelper outputHelper): AbstractDbTest(outputHelper)
{

    [Fact]
    public async Task CreateAnnotationTest()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, annotationService, bookService, chapter, _) = await Setup(unitOfWork, context, mapper);

        // No highlight or Selected Text
        await Assert.ThrowsAsync<KavitaException>(async () =>
            await annotationService.CreateAnnotation(user.Id, new AnnotationDto
            {
                XPath = null,
                ChapterId = 0,
                VolumeId = 0,
                SeriesId = 0,
                LibraryId = 0,
                OwnerUserId = 0
            }));


        // Chapter title
        const int pageNum = 1;
        const string chapterTitle = "My Chapter Title";
        bookService.GenerateTableOfContents(null!).ReturnsForAnyArgs([
            new BookChapterItem
            {
                Page = pageNum,
                Title = chapterTitle,
            }
        ]);

        var dto = await CreateSimpleAnnotation(annotationService, user, chapter);
        Assert.Equal(chapterTitle, dto.ChapterTitle);
    }

    [Fact]
    public async Task UpdateAnnotationTestFailures()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, annotationService, _, chapter, eventHub) = await Setup(unitOfWork, context, mapper);

        // Can't update without id
        await Assert.ThrowsAsync<KavitaException>(async () =>
            await annotationService.UpdateAnnotation(user.Id, new AnnotationDto
            {
                XPath = null,
                ChapterId = 0,
                VolumeId = 0,
                SeriesId = 0,
                LibraryId = 0,
                OwnerUserId = 0
            }));

        var dto = await CreateSimpleAnnotation(annotationService, user, chapter);

        // Can't update others annotations
        var otherUser = new AppUserBuilder("other", "other@localhost").Build();
        await Assert.ThrowsAsync<KavitaException>(async () => await annotationService.UpdateAnnotation(otherUser.Id, dto));
    }

    [Fact]
    public async Task UpdateAnnotationTestChanges()
    {
        var (unitOfWork, context, mapper) = await CreateDatabase();
        var (user, annotationService, _, chapter, eventHub) = await Setup(unitOfWork, context, mapper);

        var dto = await CreateSimpleAnnotation(annotationService, user, chapter);

        // Update relevant fields
        dto.ContainsSpoiler = true;
        dto.SelectedSlotIndex = 2;
        dto.Comment = "{}";
        dto.CommentHtml = "<p>Something New</p>";
        dto.CommentPlainText = "Something unrelated"; // Should not be used
        dto = await annotationService.UpdateAnnotation(user.Id, dto);

        Assert.True(dto.ContainsSpoiler);
        Assert.Equal(2, dto.SelectedSlotIndex);
        Assert.Equal("<p>Something New</p>", dto.CommentHtml);
        Assert.Equal("Something New", dto.CommentPlainText);

        // Ensure event was sent out to UI
        await eventHub.Received().SendMessageToAsync(
            MessageFactory.AnnotationUpdate,
            Arg.Any<SignalRMessage>(),
            user.Id);
    }

    [Fact]
    public async Task ExportAnnotationsCorrectExportUser()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var annotationRepo = Substitute.For<IAnnotationRepository>();
        var settingsRepo = Substitute.For<ISettingsRepository>();
        unitOfWork.AnnotationRepository.Returns(annotationRepo);
        unitOfWork.SettingsRepository.Returns(settingsRepo);

        settingsRepo.GetSettingsDtoAsync().Returns(new ServerSettingDto
        {
            HostName = "",
        });

        var annotationService = new AnnotationService(
            Substitute.For<ILogger<AnnotationService>>(),
            unitOfWork,
            Substitute.For<IBookService>(),
            Substitute.For<IEventHub>());

        await annotationService.ExportAnnotations(1);

        await annotationRepo.Received().GetFullAnnotationsByUserIdAsync(1);
        await annotationRepo.DidNotReceive().GetFullAnnotations(1, []);
    }

    [Fact]
    public async Task ExportAnnotationsCorrectExportSpecific()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var annotationRepo = Substitute.For<IAnnotationRepository>();
        var settingsRepo = Substitute.For<ISettingsRepository>();
        unitOfWork.AnnotationRepository.Returns(annotationRepo);
        unitOfWork.SettingsRepository.Returns(settingsRepo);

        settingsRepo.GetSettingsDtoAsync().Returns(new ServerSettingDto
        {
            HostName = "",
        });

        var annotationService = new AnnotationService(
            Substitute.For<ILogger<AnnotationService>>(),
            unitOfWork,
            Substitute.For<IBookService>(),
            Substitute.For<IEventHub>());

        List<int> ids = [1, 2, 3]; // Received checks pointers I think
        await annotationService.ExportAnnotations(1, ids);

        await annotationRepo.DidNotReceive().GetFullAnnotationsByUserIdAsync(1);
        await annotationRepo.Received().GetFullAnnotations(1, ids);
    }

    private static async Task<AnnotationDto> CreateSimpleAnnotation(IAnnotationService annotationService, AppUser user, Chapter chapter)
    {
        return await annotationService.CreateAnnotation(user.Id, new AnnotationDto
        {
            XPath = "",
            ChapterId = chapter.Id,
            VolumeId = chapter.VolumeId,
            SeriesId = chapter.Volume.SeriesId,
            LibraryId = chapter.Volume.Series.LibraryId,
            PageNumber = 1,
            OwnerUserId = user.Id,
            HighlightCount = 1,
            SelectedText = "Something"
        });
    }

    private static async Task<(AppUser, IAnnotationService, IBookService, Chapter, IEventHub)> Setup(
        IUnitOfWork unitOfWork,
        DataContext context,
        IMapper mapper)
    {
        var user = new AppUserBuilder("defaultAdmin", "defaultAdmin@localhost")
            .WithRole(PolicyConstants.AdminRole)
            .Build();

        context.AppUser.Add(user);
        await unitOfWork.CommitAsync();

        user = await unitOfWork.UserRepository.GetUserByIdAsync(user.Id, AppUserIncludes.DashboardStreams);
        Assert.NotNull(user);

        await new AccountService(
            null!,
            Substitute.For<ILogger<AccountService>>(),
            unitOfWork,
            mapper,
            Substitute.For<ILocalizationService>()
        ).SeedUser(user);

        var chapter = new ChapterBuilder("1")
            .Build();

        var lib = new LibraryBuilder("Manga")
            .WithAppUser(user)
            .WithSeries(new SeriesBuilder("Spice and Wolf")
                .WithVolume(new VolumeBuilder("1")
                    .WithChapter(chapter)
                    .Build())
                .Build())
            .Build();

        context.Library.Add(lib);
        await unitOfWork.CommitAsync();

        chapter.Volume = lib.Series.First().Volumes.First();
        chapter.Volume.Series = lib.Series.First();
        chapter.Volume.Series.Library = lib;


        var bookService = Substitute.For<IBookService>();
        var eventHub = Substitute.For<IEventHub>();
        var annotationService = new AnnotationService(
            Substitute.For<ILogger<AnnotationService>>(),
            unitOfWork, bookService, eventHub);

        return (user, annotationService, bookService, chapter, eventHub);
    }

}
