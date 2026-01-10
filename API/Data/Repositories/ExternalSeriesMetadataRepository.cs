using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs;
using API.DTOs.KavitaPlus.Manage;
using API.DTOs.Recommendation;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions.QueryExtensions;
using API.Helpers;
using API.Services.Plus;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;
#nullable enable

public interface IExternalSeriesMetadataRepository
{
    void Attach(ExternalSeriesMetadata metadata);
    void Attach(ExternalRating rating);
    void Attach(ExternalReview review);
    void Remove(IEnumerable<ExternalReview>? reviews);
    void Remove(IEnumerable<ExternalRating>? ratings);
    void Remove(IEnumerable<ExternalRecommendation>? recommendations);
    void Remove(ExternalSeriesMetadata metadata);
    Task<ExternalSeriesMetadata?> GetExternalSeriesMetadata(int seriesId);
    Task<bool> NeedsDataRefresh(int seriesId);
    Task<SeriesDetailPlusDto?> GetSeriesDetailPlusDto(int seriesId);
    Task LinkRecommendationsToSeries(Series series);
    Task<bool> IsBlacklistedSeries(int seriesId);
    Task<IList<int>> GetSeriesThatNeedExternalMetadata(int limit, bool includeStaleData = false);
    Task<PagedList<ManageMatchSeriesDto>> GetAllSeries(ManageMatchFilterDto filter, UserParams userParams);
}

public class ExternalSeriesMetadataRepository : IExternalSeriesMetadataRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public ExternalSeriesMetadataRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Attach(ExternalSeriesMetadata metadata)
    {
        _context.ExternalSeriesMetadata.Attach(metadata);
    }

    public void Attach(ExternalRating rating)
    {
        _context.ExternalRating.Attach(rating);
    }

    public void Attach(ExternalReview review)
    {
        _context.ExternalReview.Attach(review);
    }

    public void Remove(IEnumerable<ExternalReview>? reviews)
    {
        if (reviews == null) return;
        _context.ExternalReview.RemoveRange(reviews);
    }

    public void Remove(IEnumerable<ExternalRating>? ratings)
    {
        if (ratings == null) return;
        _context.ExternalRating.RemoveRange(ratings);
    }

    public void Remove(IEnumerable<ExternalRecommendation>? recommendations)
    {
        if (recommendations == null) return;
        _context.ExternalRecommendation.RemoveRange(recommendations);
    }

    public void Remove(ExternalSeriesMetadata? metadata)
    {
        if (metadata == null) return;
        _context.ExternalSeriesMetadata.Remove(metadata);
    }

    /// <summary>
    /// Returns the ExternalSeriesMetadata entity for the given Series including all linked tables
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    public Task<ExternalSeriesMetadata?> GetExternalSeriesMetadata(int seriesId)
    {
        return _context.ExternalSeriesMetadata
            .Where(s => s.SeriesId == seriesId)
            .Include(s => s.ExternalReviews)
            .Include(s => s.ExternalRatings.OrderBy(r => r.AverageScore))
            .Include(s => s.ExternalRecommendations.OrderBy(r => r.Id))
            .AsSplitQuery()
            .FirstOrDefaultAsync();
    }

    public async Task<bool> NeedsDataRefresh(int seriesId)
    {
        // TODO: Add unit test
        return await _context.ExternalSeriesMetadata
            .Where(s => s.SeriesId == seriesId)
            .Select(s => s.ValidUntilUtc)
            .Where(date => date < DateTime.UtcNow)
            .AnyAsync();
    }

    public async Task<SeriesDetailPlusDto?> GetSeriesDetailPlusDto(int seriesId)
    {
        // TODO: Add unit test
        var seriesDetailDto = await _context.ExternalSeriesMetadata
            .Where(m => m.SeriesId == seriesId)
            .Include(m => m.ExternalRatings)
            .Include(m => m.ExternalReviews)
            .Include(m => m.ExternalRecommendations)
            .FirstOrDefaultAsync();

        if (seriesDetailDto == null)
        {
            return null; // or handle the case when seriesDetailDto is not found
        }

        var externalSeriesRecommendations = seriesDetailDto.ExternalRecommendations
            .Where(r => r.SeriesId == null)
            .Select(r => _mapper.Map<ExternalSeriesDto>(r))
            .ToList();

        var ownedIds = seriesDetailDto.ExternalRecommendations
            .Where(r => r.SeriesId != null)
            .Select(r => r.SeriesId)
            .ToList();

        var ownedSeriesRecommendations = await _context.Series
            .Where(s => ownedIds.Contains(s.Id))
            .OrderBy(s => s.SortName.ToLower())
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .ToListAsync();

        IEnumerable<UserReviewDto> reviews = [];
        if (seriesDetailDto.ExternalReviews != null && seriesDetailDto.ExternalReviews.Any())
        {
            reviews = seriesDetailDto.ExternalReviews
                .Select(r =>
                {
                    var ret = _mapper.Map<UserReviewDto>(r);
                    ret.IsExternal = true;
                    return ret;
                })
                .OrderByDescending(r => r.Score);
        }

        IEnumerable<RatingDto> ratings = [];
        if (seriesDetailDto.ExternalRatings != null && seriesDetailDto.ExternalRatings.Count != 0)
        {
            ratings = seriesDetailDto.ExternalRatings
                .Select(r => _mapper.Map<RatingDto>(r));
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
    /// <returns></returns>
    public async Task LinkRecommendationsToSeries(Series series)
    {
        var recMatches = await _context.ExternalRecommendation
            .Where(r => r.SeriesId == null || r.SeriesId == 0)
            .Where(r => EF.Functions.Like(r.Name, series.Name) ||
                        EF.Functions.Like(r.Name, series.LocalizedName))
            .ToListAsync();

        foreach (var rec in recMatches)
        {
            rec.SeriesId = series.Id;
        }

        await _context.SaveChangesAsync();
    }

    public Task<bool> IsBlacklistedSeries(int seriesId)
    {
        return _context.Series
            .Where(s => s.Id == seriesId)
            .Select(s => s.IsBlacklisted)
            .FirstOrDefaultAsync();
    }


    public async Task<IList<int>> GetSeriesThatNeedExternalMetadata(int limit, bool includeStaleData = false)
    {
        return await _context.Series
            .Where(s => !ExternalMetadataService.NonEligibleLibraryTypes.Contains(s.Library.Type))
            .Where(s => s.Library.AllowMetadataMatching)
            .WhereIf(includeStaleData, s => s.ExternalSeriesMetadata == null || s.ExternalSeriesMetadata.ValidUntilUtc < DateTime.UtcNow)
            .Where(s => s.ExternalSeriesMetadata == null || s.ExternalSeriesMetadata.AniListId == 0)
            .Where(s => !s.IsBlacklisted && !s.DontMatch)
            .OrderByDescending(s => s.Library.Type)
            .ThenBy(s => s.NormalizedName)
            .Select(s => s.Id)
            .Take(limit)
            .ToListAsync();
    }

    public Task<PagedList<ManageMatchSeriesDto>> GetAllSeries(ManageMatchFilterDto filter, UserParams userParams)
    {
        var source =  _context.Series
            .Include(s => s.Library)
            .Include(s => s.ExternalSeriesMetadata)
            .Where(s => !ExternalMetadataService.NonEligibleLibraryTypes.Contains(s.Library.Type))
            .Where(s => s.Library.AllowMetadataMatching)
            .WhereIf(filter.LibraryType >= 0, s => s.Library.Type == (LibraryType) filter.LibraryType)
            .FilterMatchState(filter.MatchStateOption)
            .OrderBy(s => s.NormalizedName)
            .ProjectTo<ManageMatchSeriesDto>(_mapper.ConfigurationProvider);

        return PagedList<ManageMatchSeriesDto>.CreateAsync(source, userParams);
    }
}
