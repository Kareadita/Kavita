using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.Plus;
using Kavita.Common.Helpers;
using Kavita.Database.Extensions;
using Kavita.Models.DTOs;
using Kavita.Models.DTOs.KavitaPlus.Manage;
using Kavita.Models.DTOs.Recommendation;
using Kavita.Models.DTOs.SeriesDetail;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.Metadata;
using Microsoft.EntityFrameworkCore;

namespace Kavita.Database.Repositories;

public class ExternalSeriesMetadataRepository(DataContext context, IMapper mapper) : IExternalSeriesMetadataRepository
{
    public void Attach(ExternalSeriesMetadata metadata)
    {
        context.ExternalSeriesMetadata.Attach(metadata);
    }

    public void Attach(ExternalRating rating)
    {
        context.ExternalRating.Attach(rating);
    }

    public void Attach(ExternalReview review)
    {
        context.ExternalReview.Attach(review);
    }

    public void Remove(IEnumerable<ExternalReview>? reviews)
    {
        if (reviews == null) return;
        context.ExternalReview.RemoveRange(reviews);
    }

    public void Remove(IEnumerable<ExternalRating>? ratings)
    {
        if (ratings == null) return;
        context.ExternalRating.RemoveRange(ratings);
    }

    public void Remove(IEnumerable<ExternalRecommendation>? recommendations)
    {
        if (recommendations == null) return;
        context.ExternalRecommendation.RemoveRange(recommendations);
    }

    public void Remove(ExternalSeriesMetadata? metadata)
    {
        if (metadata == null) return;
        context.ExternalSeriesMetadata.Remove(metadata);
    }

    /// <summary>
    /// Returns the ExternalSeriesMetadata entity for the given Series including all linked tables
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task<ExternalSeriesMetadata?> GetExternalSeriesMetadata(int seriesId, CancellationToken ct = default)
    {
        return context.ExternalSeriesMetadata
            .Where(s => s.SeriesId == seriesId)
            .Include(s => s.ExternalReviews)
            .Include(s => s.ExternalRatings.OrderBy(r => r.AverageScore))
            .Include(s => s.ExternalRecommendations.OrderBy(r => r.Id))
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> NeedsDataRefresh(int seriesId, CancellationToken ct = default)
    {
        return await context.ExternalSeriesMetadata
            .Where(s => s.SeriesId == seriesId)
            .Select(s => s.ValidUntilUtc)
            .Where(date => date < DateTime.UtcNow)
            .AnyAsync(ct);
    }

    public async Task<SeriesDetailPlusDto?> GetSeriesDetailPlusDto(int seriesId, CancellationToken ct = default)
    {
        var seriesDetailDto = await context.ExternalSeriesMetadata
            .Where(m => m.SeriesId == seriesId)
            .Include(m => m.ExternalRatings)
            .Include(m => m.ExternalReviews)
            .Include(m => m.ExternalRecommendations)
            .FirstOrDefaultAsync(ct);

        if (seriesDetailDto == null)
        {
            return null; // or handle the case when seriesDetailDto is not found
        }

        var externalSeriesRecommendations = seriesDetailDto.ExternalRecommendations
            .Where(r => r.SeriesId == null)
            .Select(mapper.Map<ExternalSeriesDto>)
            .ToList();

        var ownedIds = seriesDetailDto.ExternalRecommendations
            .Where(r => r.SeriesId != null)
            .Select(r => r.SeriesId)
            .ToList();

        var ownedSeriesRecommendations = await context.Series
            .Where(s => ownedIds.Contains(s.Id))
            .OrderBy(s => s.SortName.ToLower())
            .ProjectTo<SeriesDto>(mapper.ConfigurationProvider)
            .ToListAsync(ct);

        IEnumerable<UserReviewDto> reviews = [];
        if (seriesDetailDto.ExternalReviews != null && seriesDetailDto.ExternalReviews.Any())
        {
            reviews = seriesDetailDto.ExternalReviews
                .Select(r =>
                {
                    var ret = mapper.Map<UserReviewDto>(r);
                    ret.IsExternal = true;
                    return ret;
                })
                .OrderByDescending(r => r.Score);
        }

        IEnumerable<RatingDto> ratings = [];
        if (seriesDetailDto.ExternalRatings != null && seriesDetailDto.ExternalRatings.Count != 0)
        {
            ratings = seriesDetailDto.ExternalRatings
                .Select(mapper.Map<RatingDto>);
        }


        var seriesDetailPlusDto = new SeriesDetailPlusDto()
        {
            Ratings = ratings,
            Reviews = reviews,
            Recommendations = new RecommendationDto()
            {
                ExternalSeries = externalSeriesRecommendations,
                OwnedSeries = ownedSeriesRecommendations
            }
        };

        return seriesDetailPlusDto;
    }

    /// <summary>
    /// Searches Recommendations without a SeriesId on record and attempts to link based on Series Name/Localized Name
    /// </summary>
    /// <param name="series"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task LinkRecommendationsToSeries(Series series, CancellationToken ct = default)
    {
        var recMatches = await context.ExternalRecommendation
            .Where(r => r.SeriesId == null || r.SeriesId == 0)
            .Where(r => EF.Functions.Like(r.Name, series.Name) ||
                        EF.Functions.Like(r.Name, series.LocalizedName))
            .ToListAsync(ct);

        foreach (var rec in recMatches)
        {
            rec.SeriesId = series.Id;
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task<IList<int>> GetSeriesThatNeedExternalMetadata(int limit, bool includeStaleData = false,
        CancellationToken ct = default)
    {
        return await context.Series
            .Where(s => !IExternalMetadataService.NonEligibleLibraryTypes.Contains(s.Library.Type))
            .Where(s => s.Library.AllowMetadataMatching)
            .WhereIf(includeStaleData, s => s.ExternalSeriesMetadata == null || s.ExternalSeriesMetadata.ValidUntilUtc < DateTime.UtcNow)
            .WhereIf(!includeStaleData, s => s.ExternalSeriesMetadata == null || s.ExternalSeriesMetadata.AniListId == 0)
            .Where(s => !s.IsBlacklisted && !s.DontMatch)
            .OrderByDescending(s => s.Library.Type)
            .ThenBy(s => s.NormalizedName)
            .Select(s => s.Id)
            .Take(limit)
            .ToListAsync(ct);
    }

    public Task<PagedList<ManageMatchSeriesDto>> GetAllSeries(ManageMatchFilterDto filter, UserParams userParams,
        CancellationToken ct = default)
    {
        var source =  context.Series
            .Include(s => s.Library)
            .Include(s => s.ExternalSeriesMetadata)
            .Where(s => !IExternalMetadataService.NonEligibleLibraryTypes.Contains(s.Library.Type))
            .Where(s => s.Library.AllowMetadataMatching)
            .WhereIf(filter.LibraryType >= 0, s => s.Library.Type == (LibraryType) filter.LibraryType)
            .FilterMatchState(filter.MatchStateOption)
            .OrderBy(s => s.NormalizedName)
            .ProjectTo<ManageMatchSeriesDto>(mapper.ConfigurationProvider);

        return PagedList<ManageMatchSeriesDto>.CreateAsync(source, userParams, ct);
    }
}
