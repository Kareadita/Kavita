using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.Metadata;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Metadata;

namespace Kavita.API.Repositories;

[Flags]
public enum ChapterIncludes
{
    None = 1 << 0,
    Volumes = 1 << 1,
    Files = 1 << 2,
    People = 1 << 3,
    Genres = 1 << 4,
    Tags = 1 << 5,
    ExternalReviews = 1 << 6,
    ExternalRatings = 1 << 7
}

public interface IChapterRepository
{
    void Update(Chapter chapter);
    void Remove(Chapter chapter);
    void Remove(IList<Chapter> chapters);
    Task<IList<Chapter>> GetChaptersByIdsAsync(IList<int> chapterIds, ChapterIncludes includes = ChapterIncludes.None, CancellationToken ct = default);
    Task<IChapterInfoDto?> GetChapterInfoDtoAsync(int chapterId, CancellationToken ct = default);
    Task<int> GetChapterTotalPagesAsync(int chapterId, CancellationToken ct = default);
    Task<Chapter?> GetChapterAsync(int chapterId, ChapterIncludes includes = ChapterIncludes.Files, CancellationToken ct = default);
    Task<ChapterDto?> GetChapterDtoAsync(int chapterId, int userId, CancellationToken ct = default);
    Task<IList<ChapterDto>> GetChapterDtoByIdsAsync(IEnumerable<int> chapterIds, int userId, CancellationToken ct = default);
    Task<ChapterMetadataDto?> GetChapterMetadataDtoAsync(int chapterId, ChapterIncludes includes = ChapterIncludes.Files, CancellationToken ct = default);
    Task<IList<MangaFile>> GetFilesForChapterAsync(int chapterId, CancellationToken ct = default);
    Task<IList<Chapter>> GetChaptersAsync(int volumeId, ChapterIncludes includes = ChapterIncludes.None, CancellationToken ct = default);
    Task<IList<ChapterDto>> GetChapterDtosAsync(int volumeId, int userId, CancellationToken ct = default);
    Task<IList<MangaFile>> GetFilesForChaptersAsync(IReadOnlyList<int> chapterIds, CancellationToken ct = default);
    Task<long> GetFilesizeAsync(int chapterId, CancellationToken ct = default);
    Task<Dictionary<int, long>> GetFilesizesAsync(IList<int> chapterIds, CancellationToken ct = default);
    Task<string?> GetChapterCoverImageAsync(int chapterId, CancellationToken ct = default);
    Task<IList<string>> GetAllCoverImagesAsync(CancellationToken ct = default);
    Task<IList<Chapter>> GetAllChaptersWithCoversInDifferentEncoding(EncodeFormat format, CancellationToken ct = default);
    Task<IEnumerable<string>> GetCoverImagesForLockedChaptersAsync(CancellationToken ct = default);
    IQueryable<Chapter> GetChaptersForSeries(int seriesId, CancellationToken ct = default);
    Task<IList<Chapter>> GetAllChaptersForSeries(int seriesId, CancellationToken ct = default);
    Task<int> GetAverageUserRating(int chapterId, int userId, CancellationToken ct = default);
    Task<IList<UserReviewDto>> GetExternalChapterReviewDtos(int chapterId, CancellationToken ct = default);
    Task<IList<ExternalReview>> GetExternalChapterReview(int chapterId, CancellationToken ct = default);
    Task<IList<RatingDto>> GetExternalChapterRatingDtos(int chapterId, CancellationToken ct = default);
    Task<IList<ExternalRating>> GetExternalChapterRatings(int chapterId, CancellationToken ct = default);
    Task<ChapterDto?> GetCurrentlyReadingChapterAsync(int seriesId, int userId, CancellationToken ct = default);
    Task<ChapterDto?> GetFirstChapterForSeriesAsync(int seriesId, int userId, CancellationToken ct = default);
    Task<ChapterDto?> GetFirstChapterForVolumeAsync(int volumeId, int userId, CancellationToken ct = default);
    Task<IList<ChapterDto>> GetChapterDtosAsync(IEnumerable<int> chapterIds, int userId, CancellationToken ct = default);
    Task<int?> GetSeriesIdForChapter(int chapterId, CancellationToken ct = default);

    /// <summary>
    /// Fetches chapters matching by ComicVineId or MetronId, with Volume and Series included.
    /// If KavitaIds are passed, will prioritize over CV/Metron.
    /// </summary>
    Task<IList<Chapter>> GetChaptersByExternalIdsAsync(IList<int> kavitaIds, IList<string> comicVineIds, IList<long> metronIds, IList<int> libraryIds, CancellationToken ct = default);

    /// <summary>
    /// Fetches chapters that have a non-empty AlternateSeries field from the specified libraries
    /// </summary>
    Task<IList<Chapter>> GetChaptersByAlternateSeriesAsync(IList<string> normalizedNames, IList<int> libraryIds, CancellationToken ct = default);
}
