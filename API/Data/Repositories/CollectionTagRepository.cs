﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.CollectionTags;
using API.Entities;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface ICollectionTagRepository
{
    void Add(CollectionTag tag);
    void Remove(CollectionTag tag);
    Task<IEnumerable<CollectionTagDto>> GetAllTagDtosAsync();
    Task<IEnumerable<CollectionTagDto>> SearchTagDtosAsync(string searchQuery);
    Task<string> GetCoverImageAsync(int collectionTagId);
    Task<IEnumerable<CollectionTagDto>> GetAllPromotedTagDtosAsync();
    Task<CollectionTag> GetTagAsync(int tagId);
    Task<CollectionTag> GetFullTagAsync(int tagId);
    void Update(CollectionTag tag);
    Task<int> RemoveTagsWithoutSeries();
    Task<IEnumerable<CollectionTag>> GetAllTagsAsync();
    Task<IList<string>> GetAllCoverImagesAsync();
}
public class CollectionTagRepository : ICollectionTagRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public CollectionTagRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Add(CollectionTag tag)
    {
        _context.CollectionTag.Add(tag);
    }

    public void Remove(CollectionTag tag)
    {
        _context.CollectionTag.Remove(tag);
    }

    public void Update(CollectionTag tag)
    {
        _context.Entry(tag).State = EntityState.Modified;
    }

    /// <summary>
    /// Removes any collection tags without any series
    /// </summary>
    public async Task<int> RemoveTagsWithoutSeries()
    {
        var tagsToDelete = await _context.CollectionTag
            .Include(c => c.SeriesMetadatas)
            .Where(c => c.SeriesMetadatas.Count == 0)
            .ToListAsync();
        _context.RemoveRange(tagsToDelete);

        return await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<CollectionTag>> GetAllTagsAsync()
    {
        return await _context.CollectionTag
            .OrderBy(c => c.NormalizedTitle)
            .ToListAsync();
    }

    public async Task<IList<string>> GetAllCoverImagesAsync()
    {
        return await _context.CollectionTag
            .Select(t => t.CoverImage)
            .Where(t => !string.IsNullOrEmpty(t))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<CollectionTagDto>> GetAllTagDtosAsync()
    {
        return await _context.CollectionTag
            .Select(c => c)
            .OrderBy(c => c.NormalizedTitle)
            .AsNoTracking()
            .ProjectTo<CollectionTagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IEnumerable<CollectionTagDto>> GetAllPromotedTagDtosAsync()
    {
        return await _context.CollectionTag
            .Where(c => c.Promoted)
            .OrderBy(c => c.NormalizedTitle)
            .AsNoTracking()
            .ProjectTo<CollectionTagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<CollectionTag> GetTagAsync(int tagId)
    {
        return await _context.CollectionTag
            .Where(c => c.Id == tagId)
            .SingleOrDefaultAsync();
    }

    public async Task<CollectionTag> GetFullTagAsync(int tagId)
    {
        return await _context.CollectionTag
            .Where(c => c.Id == tagId)
            .Include(c => c.SeriesMetadatas)
            .SingleOrDefaultAsync();
    }

    public async Task<IEnumerable<CollectionTagDto>> SearchTagDtosAsync(string searchQuery)
    {
        return await _context.CollectionTag
            .Where(s => EF.Functions.Like(s.Title, $"%{searchQuery}%")
                        || EF.Functions.Like(s.NormalizedTitle, $"%{searchQuery}%"))
            .OrderBy(s => s.Title)
            .AsNoTracking()
            .OrderBy(c => c.NormalizedTitle)
            .ProjectTo<CollectionTagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<string> GetCoverImageAsync(int collectionTagId)
    {
        return await _context.CollectionTag
            .Where(c => c.Id == collectionTagId)
            .Select(c => c.CoverImage)
            .AsNoTracking()
            .SingleOrDefaultAsync();
    }
}
