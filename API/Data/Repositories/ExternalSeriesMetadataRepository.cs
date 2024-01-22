using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Constants;
using API.DTOs;
using API.DTOs.Recommendation;
using API.DTOs.SeriesDetail;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Metadata;
using API.Extensions;
using API.Extensions.QueryExtensions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IExternalSeriesMetadataRepository
{
    void Attach(ExternalSeriesMetadata metadata);
    void Attach(ExternalRating rating);
    void Attach(ExternalReview review);
    void Remove(IEnumerable<ExternalReview>? reviews);
    void Remove(IEnumerable<ExternalRating>? ratings);
    void Remove(IEnumerable<ExternalRecommendation>? recommendations);
    Task<ExternalSeriesMetadata?> GetExternalSeriesMetadata(int seriesId, int limit = 25);
    Task<SeriesDetailPlusDto> GetSeriesDetailPlusDto(int seriesId, int libraryId, AppUser user);
    Task LinkRecommendationsToSeries(Series series);
}

public class ExternalSeriesMetadataRepository : IExternalSeriesMetadataRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;
    private readonly UserManager<AppUser> _userManager;

    public ExternalSeriesMetadataRepository(DataContext context, IMapper mapper, UserManager<AppUser> userManager)
    {
        _context = context;
        _mapper = mapper;
        _userManager = userManager;
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

    public void Remove(IEnumerable<ExternalRating> ratings)
    {
        if (ratings == null) return;
        _context.ExternalRating.RemoveRange(ratings);
    }

    public void Remove(IEnumerable<ExternalRecommendation> recommendations)
    {
        if (recommendations == null) return;
        _context.ExternalRecommendation.RemoveRange(recommendations);
    }

    /// <summary>
    /// Returns the ExternalSeriesMetadata entity for the given Series including all linked tables
    /// </summary>
    /// <param name="seriesId"></param>
    /// <returns></returns>
    public Task<ExternalSeriesMetadata?> GetExternalSeriesMetadata(int seriesId, int limit = 25)
    {
        return _context.ExternalSeriesMetadata
            .Where(s => s.SeriesId == seriesId)
            .Include(s => s.ExternalReviews.Take(25))
            .Include(s => s.ExternalRatings.Take(25))
            .Include(s => s.ExternalRecommendations.Take(25))
            .AsSplitQuery()
            .FirstOrDefaultAsync();
    }

    public async Task<SeriesDetailPlusDto> GetSeriesDetailPlusDto(int seriesId, int libraryId, AppUser user)
    {
        var canSeeExternalSeries = user is { AgeRestriction: AgeRating.NotApplicable } &&
                                   await _userManager.IsInRoleAsync(user, PolicyConstants.AdminRole);

        var allowedLibraries = await _context.Library
            .Where(library => library.AppUsers.Any(x => x.Id == user.Id))
            .Select(l => l.Id)
            .ToListAsync();

        var userRating = await _context.AppUser.GetUserAgeRestriction(user.Id);

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

        var externalSeriesRecommendations = new List<ExternalSeriesDto>();
        if (!canSeeExternalSeries)
        {
            externalSeriesRecommendations = seriesDetailDto.ExternalRecommendations
                .Where(r => r.SeriesId is null or 0)
                .Select(r => _mapper.Map<ExternalSeriesDto>(r))
                .ToList();
        }


        var ownedSeriesRecommendations = await _context.ExternalRecommendation
            .Where(r => r.SeriesId > 0 && allowedLibraries.Contains(r.Series.LibraryId))
            .Join(_context.Series, r => r.SeriesId, s => s.Id, (recommendation, series) => series)
            .RestrictAgainstAgeRestriction(userRating)
            .OrderBy(s => s.SortName.ToLower())
            .ProjectTo<SeriesDto>(_mapper.ConfigurationProvider)
            .AsNoTracking()
            .ToListAsync();

        var seriesDetailPlusDto = new SeriesDetailPlusDto()
        {
            Ratings = seriesDetailDto.ExternalRatings.Select(r => _mapper.Map<RatingDto>(r)),
            Reviews = seriesDetailDto.ExternalReviews.OrderByDescending(r => r.Score).Select(r => _mapper.Map<UserReviewDto>(r)),
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
}
