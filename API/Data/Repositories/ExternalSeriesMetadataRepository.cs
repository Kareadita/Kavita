using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Entities.Metadata;
using AutoMapper;
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
    Task<ExternalSeriesMetadata?> GetExternalSeriesMetadata(int seriesId);
    Task<IList<ExternalReview>> GetExternalReviews(int seriesId);
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
    public Task<ExternalSeriesMetadata?> GetExternalSeriesMetadata(int seriesId)
    {
        return _context.ExternalSeriesMetadata
            .Where(s => s.SeriesId == seriesId)
            .Include(s => s.ExternalReviews)
            .Include(s => s.ExternalRatings)
            .Include(s => s.ExternalRecommendations)
            .AsSplitQuery()
            .FirstOrDefaultAsync();
    }

    public async Task<IList<ExternalReview>> GetExternalReviews(int seriesId)
    {
        return await _context.ExternalReview
            .Where(r => r.SeriesId == seriesId)
            .ToListAsync();
    }

}
