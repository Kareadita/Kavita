using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.DTOs;
using API.DTOs.KavitaPlus.Manage;
using API.DTOs.Recommendation;
using API.DTOs.Scrobbling;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Extensions.QueryExtensions;
using API.Services.Plus;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Identity;
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
    Task<IList<ManageMatchSeriesDto>> GetAllSeries(ManageMatchFilterDto filter);
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
        var row = await _context.ExternalSeriesMetadata
            .Where(s => s.SeriesId == seriesId)
            .FirstOrDefaultAsync();
        return row == null || row.ValidUntilUtc <= DateTime.UtcNow;
    }

    public async Task<SeriesDetailPlusDto?> GetSeriesDetailPlusDto(int seriesId)
    {
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

        IEnumerable<UserReviewDto> reviews = new List<UserReviewDto>();
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

        IEnumerable<RatingDto> ratings = new List<RatingDto>();
        if (seriesDetailDto.ExternalRatings != null && seriesDetailDto.ExternalRatings.Any())
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
            .WhereIf(includeStaleData, s => s.ExternalSeriesMetadata == null || s.ExternalSeriesMetadata.ValidUntilUtc < DateTime.UtcNow)
            .Where(s => s.ExternalSeriesMetadata == null || s.ExternalSeriesMetadata.ValidUntilUtc == DateTime.MinValue)
            .Where(s => s.Library.AllowMetadataMatching)
            .OrderByDescending(s => s.Library.Type)
            .ThenBy(s => s.NormalizedName)
            .Select(s => s.Id)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IList<ManageMatchSeriesDto>> GetAllSeries(ManageMatchFilterDto filter)
    {
        return await _context.Series
            .Include(s => s.Library)
            .Include(s => s.ExternalSeriesMetadata)
            .Where(s => !ExternalMetadataService.NonEligibleLibraryTypes.Contains(s.Library.Type))
            .FilterMatchState(filter.MatchStateOption)
            .OrderBy(s => s.NormalizedName)
            .ProjectTo<ManageMatchSeriesDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }
}
