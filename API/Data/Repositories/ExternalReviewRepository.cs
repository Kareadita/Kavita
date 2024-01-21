using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Entities.Metadata;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface IExternalReviewRepository
{
    void Attach(ExternalReview review);
    void Remove(IEnumerable<ExternalReview> reviews);
    Task<IList<ExternalReview>> GetExternalReviews(int seriesId);
}

public class ExternalReviewRepository : IExternalReviewRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public ExternalReviewRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Attach(ExternalReview review)
    {
        _context.Attach(review);
    }

    public void Remove(IEnumerable<ExternalReview> reviews)
    {
        _context.RemoveRange(reviews);
    }

    public async Task<IList<ExternalReview>> GetExternalReviews(int seriesId)
    {
        return await _context.ExternalReview
            .Where(r => r.SeriesId == seriesId)
            .ToListAsync();
    }
}
