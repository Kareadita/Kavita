using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.Entities.Enums;
using API.Entities.User;
using API.Services.Reading;
using API.Data;
using API.Data.Repositories;
using API.Services;
using API.Services.Plus;
using API.SignalR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using System.IO.Abstractions.TestingHelpers;

namespace API.Tests.Services;

public class ReaderServiceRereadTests
{
    private readonly ISeriesRepository _seriesRepo;
    private readonly IVolumeRepository _volumeRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly IAppUserProgressRepository _progressRepo;
    private readonly IUserRepository _userRepo;
    private readonly ILibraryRepository _libraryRepo;
    private readonly ISeriesService _seriesService;
    private readonly ReaderService _readerService;

    public ReaderServiceRereadTests()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        _seriesRepo = Substitute.For<ISeriesRepository>();
        _volumeRepo = Substitute.For<IVolumeRepository>();
        _chapterRepo = Substitute.For<IChapterRepository>();
        _progressRepo = Substitute.For<IAppUserProgressRepository>();
        _userRepo = Substitute.For<IUserRepository>();
        _libraryRepo = Substitute.For<ILibraryRepository>();
        _seriesService = Substitute.For<ISeriesService>();

        unitOfWork.SeriesRepository.Returns(_seriesRepo);
        unitOfWork.VolumeRepository.Returns(_volumeRepo);
        unitOfWork.ChapterRepository.Returns(_chapterRepo);
        unitOfWork.AppUserProgressRepository.Returns(_progressRepo);
        unitOfWork.UserRepository.Returns(_userRepo);
        unitOfWork.LibraryRepository.Returns(_libraryRepo);

        _readerService = new ReaderService(
            unitOfWork,
            Substitute.For<ILogger<ReaderService>>(),
            Substitute.For<IEventHub>(),
            Substitute.For<IImageService>(),
            new DirectoryService(Substitute.For<ILogger<DirectoryService>>(), new MockFileSystem()),
            Substitute.For<IScrobblingService>(),
            Substitute.For<IReadingSessionService>(),
            Substitute.For<IClientInfoAccessor>(),
            _seriesService,
            Substitute.For<IEntityNamingService>(),
            Substitute.For<ILocalizationService>()
        );
    }

    #region CheckSeriesForReRead Tests

    [Fact]
    public async Task CheckSeriesForReRead_NoProgress_ShouldNotPrompt()
    {
        // Arrange
        const int userId = 1;
        const int seriesId = 1;
        const int libraryId = 1;

        var seriesDto = new SeriesDto
        {
            Id = seriesId,
            Name = "Test Series",
            PagesRead = 0,
            Pages = 100,
            LibraryId = libraryId
        };
        var continuePoint = new ChapterDto
        {
            Id = 1,
            VolumeId = 1,
            PagesRead = 0,
            Pages = 10,
            Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
        };

        _seriesRepo.GetSeriesDtoByIdAsync(seriesId, userId).Returns(seriesDto);
        _libraryRepo.GetLibraryTypeAsync(libraryId).Returns(LibraryType.Manga);
        _chapterRepo.GetFirstChapterForSeriesAsync(seriesId, userId).Returns(continuePoint);
        _progressRepo.AnyUserProgressForSeriesAsync(seriesId, userId).Returns(false);
        _progressRepo.GetLatestProgressForSeries(seriesId, userId).Returns((DateTime?)null);

        // Act
        var result = await _readerService.CheckSeriesForReRead(userId, seriesId, libraryId);

        // Assert
        Assert.False(result.ShouldPrompt);
        Assert.NotNull(result.ChapterOnContinue);
        Assert.Equal(continuePoint.Id, result.ChapterOnContinue.ChapterId);
    }

    [Fact]
    public async Task CheckSeriesForReRead_FullyRead_ShouldPromptFullReread()
    {
        // Arrange
        const int userId = 1;
        const int seriesId = 1;
        const int libraryId = 1;

        var seriesDto = new SeriesDto
        {
            Id = seriesId,
            Name = "Test Series",
            PagesRead = 100,
            Pages = 100,
            LibraryId = libraryId
        };
        var continuePoint = new ChapterDto
        {
            Id = 10,
            VolumeId = 3,
            PagesRead = 10,
            Pages = 10,
            Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
        };
        var firstChapter = new ChapterDto
        {
            Id = 1,
            VolumeId = 1,
            PagesRead = 10,
            Pages = 10,
            Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
        };

        _seriesRepo.GetSeriesDtoByIdAsync(seriesId, userId).Returns(seriesDto);
        _libraryRepo.GetLibraryTypeAsync(libraryId).Returns(LibraryType.Manga);
        _chapterRepo.GetFirstChapterForSeriesAsync(seriesId, userId).Returns(firstChapter);
        _progressRepo.AnyUserProgressForSeriesAsync(seriesId, userId).Returns(true);
        _progressRepo.GetLatestProgressForSeries(seriesId, userId).Returns(DateTime.UtcNow.AddDays(-1));

        // Mock GetContinuePoint internals
        _progressRepo.AnyUserProgressForSeriesAsync(seriesId, userId).Returns(true);
        _chapterRepo.GetCurrentlyReadingChapterAsync(seriesId, userId).Returns(continuePoint);

        // Act
        var result = await _readerService.CheckSeriesForReRead(userId, seriesId, libraryId);

        // Assert
        Assert.True(result.ShouldPrompt);
        Assert.True(result.FullReread);
        Assert.False(result.TimePrompt);
        Assert.NotNull(result.ChapterOnReread);
        Assert.Equal(firstChapter.Id, result.ChapterOnReread.ChapterId);
        Assert.Equal(seriesDto.Name, result.ChapterOnReread.Label);
    }

    [Fact]
    public async Task CheckSeriesForReRead_ContinuePointHasProgress_NotFullyRead_ShouldNotPrompt()
    {
        // Arrange
        const int userId = 1;
        const int seriesId = 1;
        const int libraryId = 1;

        var userPrefs = new AppUserPreferences
        {
            PromptForRereadsAfter = 30,
            Theme = null!,
        };
        var seriesDto = new SeriesDto
        {
            Id = seriesId,
            Name = "Test Series",
            PagesRead = 50,
            Pages = 100,
            LibraryId = libraryId
        };
        var continuePoint = new ChapterDto
        {
            Id = 5,
            VolumeId = 2,
            PagesRead = 5,
            Pages = 10,
            Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
        };

        _seriesRepo.GetSeriesDtoByIdAsync(seriesId, userId).Returns(seriesDto);
        _libraryRepo.GetLibraryTypeAsync(libraryId).Returns(LibraryType.Manga);
        _userRepo.GetPreferencesForUser(userId).Returns(userPrefs);
        _progressRepo.AnyUserProgressForSeriesAsync(seriesId, userId).Returns(true);
        _progressRepo.GetLatestProgressForSeries(seriesId, userId).Returns(DateTime.UtcNow.AddDays(-1));
        _chapterRepo.GetCurrentlyReadingChapterAsync(seriesId, userId).Returns(continuePoint);

        // Act
        var result = await _readerService.CheckSeriesForReRead(userId, seriesId, libraryId);

        // Assert
        Assert.False(result.ShouldPrompt);
        Assert.Equal(continuePoint.Id, result.ChapterOnContinue.ChapterId);
        Assert.Equal(continuePoint.Id, result.ChapterOnReread.ChapterId);
    }

    [Fact]
    public async Task CheckSeriesForReRead_ContinuePointHasProgress_FullyRead_ShouldPrompt()
    {
        // Arrange
        const int userId = 1;
        const int seriesId = 1;
        const int libraryId = 1;

        var userPrefs = new AppUserPreferences
        {
            PromptForRereadsAfter = 30,
            Theme = null!,
        };
        var seriesDto = new SeriesDto
        {
            Id = seriesId,
            Name = "Test Series",
            PagesRead = 50,
            Pages = 100,
            LibraryId = libraryId
        };
        var continuePoint = new ChapterDto
        {
            Id = 5,
            VolumeId = 2,
            PagesRead = 10,
            Pages = 10,
            Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
        };

        _seriesRepo.GetSeriesDtoByIdAsync(seriesId, userId).Returns(seriesDto);
        _libraryRepo.GetLibraryTypeAsync(libraryId).Returns(LibraryType.Manga);
        _userRepo.GetPreferencesForUser(userId).Returns(userPrefs);
        _progressRepo.AnyUserProgressForSeriesAsync(seriesId, userId).Returns(true);
        _progressRepo.GetLatestProgressForSeries(seriesId, userId).Returns(DateTime.UtcNow.AddDays(-1));
        _chapterRepo.GetCurrentlyReadingChapterAsync(seriesId, userId).Returns(continuePoint);

        // Act
        var result = await _readerService.CheckSeriesForReRead(userId, seriesId, libraryId);

        // Assert
        Assert.True(result.ShouldPrompt);
        Assert.False(result.TimePrompt);
        Assert.Equal(continuePoint.Id, result.ChapterOnContinue.ChapterId);
        Assert.Equal(continuePoint.Id, result.ChapterOnReread.ChapterId);
    }

    [Fact]
    public async Task CheckSeriesForReRead_LongTimeSinceLastProgress_ShouldPromptTimeBasedReread()
    {
        // Arrange
        const int userId = 1;
        const int seriesId = 1;
        const int libraryId = 1;
        const int daysSinceRead = 50;

        var userPrefs = new AppUserPreferences
        {
            PromptForRereadsAfter = 30,
            Theme = null!,
        };
        var seriesDto = new SeriesDto
        {
            Id = seriesId,
            Name = "Test Series",
            PagesRead = 50,
            Pages = 100,
            LibraryId = libraryId
        };
        var continuePoint = new ChapterDto
        {
            Id = 5,
            VolumeId = 2,
            PagesRead = 5,
            Pages = 10,
            Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
        };

        _seriesRepo.GetSeriesDtoByIdAsync(seriesId, userId).Returns(seriesDto);
        _libraryRepo.GetLibraryTypeAsync(libraryId).Returns(LibraryType.Manga);
        _userRepo.GetPreferencesForUser(userId).Returns(userPrefs);
        _progressRepo.AnyUserProgressForSeriesAsync(seriesId, userId).Returns(true);
        _progressRepo.GetLatestProgressForSeries(seriesId, userId).Returns(DateTime.UtcNow.AddDays(-daysSinceRead));
        _chapterRepo.GetCurrentlyReadingChapterAsync(seriesId, userId).Returns(continuePoint);

        // Act
        var result = await _readerService.CheckSeriesForReRead(userId, seriesId, libraryId);

        // Assert
        Assert.True(result.ShouldPrompt);
        Assert.True(result.TimePrompt);
        Assert.Equal(daysSinceRead, result.DaysSinceLastRead);
        Assert.Equal(continuePoint.Id, result.ChapterOnContinue.ChapterId);
        Assert.Equal(continuePoint.Id, result.ChapterOnReread.ChapterId);
    }

    [Fact]
    public async Task CheckSeriesForReRead_ContinuePointNoProgress_WithPreviousChapter_ShouldOfferPrevChapter()
    {
        // Arrange
        const int userId = 1;
        const int seriesId = 1;
        const int libraryId = 1;
        const int daysSinceRead = 50;

        var userPrefs = new AppUserPreferences
        {
            PromptForRereadsAfter = 30,
            Theme = null!,
        };
        var seriesDto = new SeriesDto
        {
            Id = seriesId,
            Name = "Test Series",
            PagesRead = 50,
            Pages = 100,
            LibraryId = libraryId
        };
        var continuePoint = new ChapterDto
        {
            Id = 5,
            SortOrder = 2,
            VolumeId = 2,
            PagesRead = 0,
            Pages = 10,
            Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
        };
        var prevChapter = new ChapterDto
        {
            Id = 4,
            SortOrder = 1,
            VolumeId = 2,
            PagesRead = 10,
            Pages = 10,
            Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
        };

        _seriesRepo.GetSeriesDtoByIdAsync(seriesId, userId).Returns(seriesDto);
        _libraryRepo.GetLibraryTypeAsync(libraryId).Returns(LibraryType.Manga);
        _userRepo.GetPreferencesForUser(userId).Returns(userPrefs);
        _progressRepo.AnyUserProgressForSeriesAsync(seriesId, userId).Returns(true);
        _progressRepo.GetLatestProgressForSeries(seriesId, userId).Returns(DateTime.UtcNow.AddDays(-daysSinceRead));
        _chapterRepo.GetCurrentlyReadingChapterAsync(seriesId, userId).Returns(continuePoint);
        _chapterRepo.GetChapterDtoAsync(4, userId).Returns(prevChapter);

        // Mock GetPrevChapterIdAsync to return chapter 4
        var volumes = new List<VolumeDto>
        {
            new VolumeDto
            {
                Id = 2,
                MinNumber = 2,
                Chapters = new List<ChapterDto>
                {
                    prevChapter,
                    continuePoint
                }
            }
        };
        _volumeRepo.GetVolumesDtoAsync(seriesId, userId).Returns(volumes);

        // Act
        var result = await _readerService.CheckSeriesForReRead(userId, seriesId, libraryId);

        // Assert
        Assert.True(result.ShouldPrompt);
        Assert.True(result.TimePrompt);
        Assert.Equal(daysSinceRead, result.DaysSinceLastRead);
        Assert.Equal(continuePoint.Id, result.ChapterOnContinue.ChapterId);
        Assert.Equal(prevChapter.Id, result.ChapterOnReread.ChapterId);
    }

    [Fact]
    public async Task CheckSeriesForReRead_SeriesNotFound_ShouldReturnDont()
    {
        // Arrange
        const int userId = 1;
        const int seriesId = 999;
        const int libraryId = 1;

        _seriesRepo.GetSeriesDtoByIdAsync(seriesId, userId).Returns((SeriesDto?)null);

        // Act
        var result = await _readerService.CheckSeriesForReRead(userId, seriesId, libraryId);

        // Assert
        Assert.False(result.ShouldPrompt);
    }

    #endregion

    #region CheckVolumeForReRead Tests

    [Fact]
    public async Task CheckVolumeForReRead_NoProgress_ShouldNotPrompt()
    {
        // Arrange
        const int userId = 1;
        const int volumeId = 1;
        const int seriesId = 1;
        const int libraryId = 1;

        var userPrefs = new AppUserPreferences
        {
            PromptForRereadsAfter = 30,
            Theme = null!,
        };
        var volumeDto = new VolumeDto
        {
            Id = volumeId,
            PagesRead = 0,
            Pages = 100,
            Chapters = new List<ChapterDto>
            {
                new ChapterDto
                {
                    Id = 1,
                    VolumeId = volumeId,
                    PagesRead = 0,
                    Pages = 10,
                    Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
                }
            }
        };

        _userRepo.GetPreferencesForUser(userId).Returns(userPrefs);
        _volumeRepo.GetVolumeDtoAsync(volumeId, userId).Returns(volumeDto);
        _libraryRepo.GetLibraryTypeAsync(libraryId).Returns(LibraryType.Manga);
        _progressRepo.GetLatestProgressForVolume(volumeId, userId).Returns((DateTime?)null);

        // Act
        var result = await _readerService.CheckVolumeForReRead(userId, volumeId, seriesId, libraryId);

        // Assert
        Assert.False(result.ShouldPrompt);
        Assert.Equal(volumeDto.Chapters.First().Id, result.ChapterOnContinue.ChapterId);
    }

    [Fact]
    public async Task CheckVolumeForReRead_FullyRead_ShouldPromptFullReread()
    {
        // Arrange
        const int userId = 1;
        const int volumeId = 1;
        const int seriesId = 1;
        const int libraryId = 1;

        var userPrefs = new AppUserPreferences
        {
            PromptForRereadsAfter = 30,
            Theme = null!,
        };
        var volumeDto = new VolumeDto
        {
            Id = volumeId,
            Name = "Volume 1",
            PagesRead = 100,
            Pages = 100,
            Chapters = new List<ChapterDto>
            {
                new ChapterDto
                {
                    Id = 1,
                    VolumeId = volumeId,
                    PagesRead = 50,
                    Pages = 50,
                    Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
                },
                new ChapterDto
                {
                    Id = 2,
                    VolumeId = volumeId,
                    PagesRead = 50,
                    Pages = 50,
                    Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
                }
            }
        };
        var firstChapter = volumeDto.Chapters.First();

        _userRepo.GetPreferencesForUser(userId).Returns(userPrefs);
        _volumeRepo.GetVolumeDtoAsync(volumeId, userId).Returns(volumeDto);
        _libraryRepo.GetLibraryTypeAsync(libraryId).Returns(LibraryType.Manga);
        _progressRepo.GetLatestProgressForVolume(volumeId, userId).Returns(DateTime.UtcNow.AddDays(-1));
        _chapterRepo.GetFirstChapterForVolumeAsync(volumeId, userId).Returns(firstChapter);
        // Act
        var result = await _readerService.CheckVolumeForReRead(userId, volumeId, seriesId, libraryId);

        // Assert
        Assert.True(result.ShouldPrompt);
        Assert.True(result.FullReread);
        Assert.False(result.TimePrompt);
        Assert.NotNull(result.ChapterOnReread);
        Assert.Equal(firstChapter.Id, result.ChapterOnReread.ChapterId);
    }

    [Fact]
    public async Task CheckVolumeForReRead_PartialProgressInVolume_ShouldCheckTime()
    {
        // Arrange
        const int userId = 1;
        const int volumeId = 1;
        const int seriesId = 1;
        const int libraryId = 1;
        const int daysSinceRead = 10;

        var userPrefs = new AppUserPreferences
        {
            PromptForRereadsAfter = 30,
            Theme = null!,
        };
        var volumeDto = new VolumeDto
        {
            Id = volumeId,
            PagesRead = 50,
            Pages = 100,
            Chapters = new List<ChapterDto>
            {
                new ChapterDto
                {
                    Id = 1,
                    VolumeId = volumeId,
                    PagesRead = 50,
                    Pages = 50,
                    Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
                    SortOrder = 1
                },
                new ChapterDto
                {
                    Id = 2,
                    VolumeId = volumeId,
                    PagesRead = 0,
                    Pages = 50,
                    Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
                    SortOrder = 2
                }
            }
        };

        _userRepo.GetPreferencesForUser(userId).Returns(userPrefs);
        _volumeRepo.GetVolumeDtoAsync(volumeId, userId).Returns(volumeDto);
        _volumeRepo.GetVolumesDtoAsync(seriesId, userId).Returns([volumeDto]);
        _libraryRepo.GetLibraryTypeAsync(libraryId).Returns(LibraryType.Manga);
        _progressRepo.GetLatestProgressForVolume(volumeId, userId).Returns(DateTime.UtcNow.AddDays(-daysSinceRead));

        // Act
        var result = await _readerService.CheckVolumeForReRead(userId, volumeId, seriesId, libraryId);

        // Assert
        Assert.False(result.ShouldPrompt); // Should not prompt because days < PromptForRereadsAfter
        Assert.Equal(volumeDto.Chapters.Skip(1).First().Id, result.ChapterOnContinue.ChapterId);
    }

    [Fact]
    public async Task CheckVolumeForReRead_VolumeNotFound_ShouldReturnDont()
    {
        // Arrange
        const int userId = 1;
        const int volumeId = 999;
        const int seriesId = 1;
        const int libraryId = 1;

        var userPrefs = new AppUserPreferences
        {
            PromptForRereadsAfter = 30,
            Theme = null!,
        };
        _userRepo.GetPreferencesForUser(userId).Returns(userPrefs);
        _volumeRepo.GetVolumeDtoAsync(volumeId, userId).Returns((VolumeDto?)null);

        // Act
        var result = await _readerService.CheckVolumeForReRead(userId, volumeId, seriesId, libraryId);

        // Assert
        Assert.False(result.ShouldPrompt);
    }

    #endregion

    #region CheckChapterForReRead Tests

    [Fact]
    public async Task CheckChapterForReRead_NoProgress_ShouldNotPrompt()
    {
        // Arrange
        const int userId = 1;
        const int chapterId = 1;
        const int seriesId = 1;
        const int libraryId = 1;

        var userPrefs = new AppUserPreferences
        {
            PromptForRereadsAfter = 30,
            Theme = null!,
        };
        var chapterDto = new ChapterDto
        {
            Id = chapterId,
            VolumeId = 1,
            PagesRead = 0,
            Pages = 10,
            Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
        };

        _userRepo.GetPreferencesForUser(userId).Returns(userPrefs);
        _chapterRepo.GetChapterDtoAsync(chapterId, userId).Returns(chapterDto);
        _libraryRepo.GetLibraryTypeAsync(libraryId).Returns(LibraryType.Manga);
        _progressRepo.GetLatestProgressForChapter(chapterId, userId).Returns((DateTime?)null);

        // Act
        var result = await _readerService.CheckChapterForReRead(userId, chapterId, seriesId, libraryId);

        // Assert
        Assert.False(result.ShouldPrompt);
        Assert.Equal(chapterId, result.ChapterOnContinue.ChapterId);
    }

    [Fact]
    public async Task CheckChapterForReRead_FullyRead_ShouldPrompt()
    {
        // Arrange
        const int userId = 1;
        const int chapterId = 1;
        const int seriesId = 1;
        const int libraryId = 1;

        var userPrefs = new AppUserPreferences
        {
            PromptForRereadsAfter = 30,
            Theme = null!,
        };
        var chapterDto = new ChapterDto
        {
            Id = chapterId,
            VolumeId = 1,
            PagesRead = 10,
            Pages = 10,
            Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
        };

        _userRepo.GetPreferencesForUser(userId).Returns(userPrefs);
        _chapterRepo.GetChapterDtoAsync(chapterId, userId).Returns(chapterDto);
        _libraryRepo.GetLibraryTypeAsync(libraryId).Returns(LibraryType.Manga);
        _progressRepo.GetLatestProgressForChapter(chapterId, userId).Returns(DateTime.UtcNow.AddDays(-1));

        // Act
        var result = await _readerService.CheckChapterForReRead(userId, chapterId, seriesId, libraryId);

        // Assert
        Assert.True(result.ShouldPrompt);
        Assert.False(result.TimePrompt);
        Assert.Equal(chapterId, result.ChapterOnContinue.ChapterId);
        Assert.Equal(chapterId, result.ChapterOnReread.ChapterId);
    }

    [Fact]
    public async Task CheckChapterForReRead_PartiallyRead_WithinTimeThreshold_ShouldNotPrompt()
    {
        // Arrange
        const int userId = 1;
        const int chapterId = 1;
        const int seriesId = 1;
        const int libraryId = 1;
        const int daysSinceRead = 10;

        var userPrefs = new AppUserPreferences
        {
            PromptForRereadsAfter = 30,
            Theme = null!,
        };
        var chapterDto = new ChapterDto
        {
            Id = chapterId,
            VolumeId = 1,
            PagesRead = 5,
            Pages = 10,
            Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
        };

        _userRepo.GetPreferencesForUser(userId).Returns(userPrefs);
        _chapterRepo.GetChapterDtoAsync(chapterId, userId).Returns(chapterDto);
        _libraryRepo.GetLibraryTypeAsync(libraryId).Returns(LibraryType.Manga);
        _progressRepo.GetLatestProgressForChapter(chapterId, userId).Returns(DateTime.UtcNow.AddDays(-daysSinceRead));

        // Act
        var result = await _readerService.CheckChapterForReRead(userId, chapterId, seriesId, libraryId);

        // Assert
        Assert.False(result.ShouldPrompt);
        Assert.Equal(chapterId, result.ChapterOnContinue.ChapterId);
    }

    [Fact]
    public async Task CheckChapterForReRead_PartiallyRead_BeyondTimeThreshold_ShouldPromptTimeBasedReread()
    {
        // Arrange
        const int userId = 1;
        const int chapterId = 1;
        const int seriesId = 1;
        const int libraryId = 1;
        const int daysSinceRead = 50;

        var userPrefs = new AppUserPreferences
        {
            PromptForRereadsAfter = 30,
            Theme = null!,
        };
        var chapterDto = new ChapterDto
        {
            Id = chapterId,
            VolumeId = 1,
            PagesRead = 5,
            Pages = 10,
            Files = [new MangaFileDto(){Format = MangaFormat.Archive}],
        };

        _userRepo.GetPreferencesForUser(userId).Returns(userPrefs);
        _chapterRepo.GetChapterDtoAsync(chapterId, userId).Returns(chapterDto);
        _libraryRepo.GetLibraryTypeAsync(libraryId).Returns(LibraryType.Manga);
        _progressRepo.GetLatestProgressForChapter(chapterId, userId).Returns(DateTime.UtcNow.AddDays(-daysSinceRead));

        // Act
        var result = await _readerService.CheckChapterForReRead(userId, chapterId, seriesId, libraryId);

        // Assert
        Assert.True(result.ShouldPrompt);
        Assert.True(result.TimePrompt);
        Assert.Equal(daysSinceRead, result.DaysSinceLastRead);
        Assert.Equal(chapterId, result.ChapterOnContinue.ChapterId);
        Assert.Equal(chapterId, result.ChapterOnReread.ChapterId);
    }

    [Fact]
    public async Task CheckChapterForReRead_ChapterNotFound_ShouldReturnDont()
    {
        // Arrange
        const int userId = 1;
        const int chapterId = 999;
        const int seriesId = 1;
        const int libraryId = 1;

        var userPrefs = new AppUserPreferences
        {
            PromptForRereadsAfter = 30,
            Theme = null!,
        };
        _userRepo.GetPreferencesForUser(userId).Returns(userPrefs);
        _chapterRepo.GetChapterDtoAsync(chapterId, userId).Returns((ChapterDto?)null);

        // Act
        var result = await _readerService.CheckChapterForReRead(userId, chapterId, seriesId, libraryId);

        // Assert
        Assert.False(result.ShouldPrompt);
    }

    #endregion

}
