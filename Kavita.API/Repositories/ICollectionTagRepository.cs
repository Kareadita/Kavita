using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kavita.Common.Helpers;
using Kavita.Models.DTOs.Collection;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.User;

namespace Kavita.API.Repositories;

[Flags]
public enum CollectionTagIncludes
{
    None = 1 << 0,
    SeriesMetadata = 1 << 1,
    SeriesMetadataWithSeries = 1 << 2
}

[Flags]
public enum CollectionIncludes
{
    None = 1 << 0,
    Series = 1 << 1,
}

public interface ICollectionTagRepository
{
    void Remove(AppUserCollection tag);
    void Update(AppUserCollection tag);
    Task<string?> GetCoverImageAsync(int collectionTagId, CancellationToken ct = default);
    Task<AppUserCollection?> GetCollectionAsync(int tagId, CollectionIncludes includes = CollectionIncludes.None, CancellationToken ct = default);
    Task<int> RemoveCollectionsWithoutSeries(CancellationToken ct = default);
    Task<AppUserCollectionDto?> GetCollectionDtoAsync(int collectionId, int userId, CancellationToken ct = default);

    Task<IEnumerable<AppUserCollection>> GetAllCollectionsAsync(CollectionIncludes includes = CollectionIncludes.None, CancellationToken ct = default);

    /// <summary>
    /// Returns all of the user's collections with the option of other user's promoted
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="includePromoted"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<IEnumerable<AppUserCollectionDto>> GetCollectionDtosAsync(int userId, bool includePromoted = false, CancellationToken ct = default);
    Task<PagedList<AppUserCollectionDto>> GetCollectionDtosPagedAsync(int userId, UserParams userParams, bool includePromoted = false, CancellationToken ct = default);
    Task<IEnumerable<AppUserCollectionDto>> GetCollectionDtosBySeriesAsync(int userId, int seriesId, bool includePromoted = false, CancellationToken ct = default);

    Task<IList<string>> GetAllCoverImagesAsync(CancellationToken ct = default);
    Task<bool> CollectionExists(string title, int userId, CancellationToken ct = default);
    Task<IList<AppUserCollection>> GetAllWithCoversInDifferentEncoding(EncodeFormat encodeFormat, CancellationToken ct = default);
    Task<IList<string>> GetRandomCoverImagesAsync(int collectionId, CancellationToken ct = default);
    Task<IList<AppUserCollection>> GetCollectionsForUserAsync(int userId, CollectionIncludes includes = CollectionIncludes.None, CancellationToken ct = default);
    Task UpdateCollectionAgeRating(AppUserCollection tag, CancellationToken ct = default);
    Task<IEnumerable<AppUserCollection>> GetCollectionsByIds(IEnumerable<int> tags, CollectionIncludes includes = CollectionIncludes.None, CancellationToken ct = default);
    Task<IList<AppUserCollection>> GetAllCollectionsForSyncing(DateTime expirationTime, CancellationToken ct = default);
}
