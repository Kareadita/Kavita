﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.DTOs.Metadata;
using API.Entities;
using API.Extensions;
using API.Extensions.QueryExtensions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace API.Data.Repositories;

public interface ITagRepository
{
    void Attach(Tag tag);
    void Remove(Tag tag);
    Task<IList<Tag>> GetAllTagsAsync();
    Task<IList<Tag>> GetAllTagsByNameAsync(IEnumerable<string> normalizedNames);
    Task<IList<TagDto>> GetAllTagDtosAsync(int userId);
    Task RemoveAllTagNoLongerAssociated();
    Task<IList<TagDto>> GetAllTagDtosForLibrariesAsync(int userId, IList<int>? libraryIds = null);
}

public class TagRepository : ITagRepository
{
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public TagRepository(DataContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public void Attach(Tag tag)
    {
        _context.Tag.Attach(tag);
    }

    public void Remove(Tag tag)
    {
        _context.Tag.Remove(tag);
    }

    public async Task RemoveAllTagNoLongerAssociated()
    {
        var tagsWithNoConnections = await _context.Tag
            .Include(p => p.SeriesMetadatas)
            .Include(p => p.Chapters)
            .Where(p => p.SeriesMetadatas.Count == 0 && p.Chapters.Count == 0)
            .AsSplitQuery()
            .ToListAsync();

        _context.Tag.RemoveRange(tagsWithNoConnections);

        await _context.SaveChangesAsync();
    }

    public async Task<IList<TagDto>> GetAllTagDtosForLibrariesAsync(int userId, IList<int>? libraryIds = null)
    {
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);
        var userLibs = await _context.Library.GetUserLibraries(userId).ToListAsync();

        if (libraryIds is {Count: > 0})
        {
            userLibs = userLibs.Where(libraryIds.Contains).ToList();
        }

        return await _context.Series
            .Where(s => userLibs.Contains(s.LibraryId))
            .RestrictAgainstAgeRestriction(userRating)
            .SelectMany(s => s.Metadata.Tags)
            .AsSplitQuery()
            .Distinct()
            .OrderBy(t => t.NormalizedTitle)
            .AsNoTracking()
            .ProjectTo<TagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }

    public async Task<IList<Tag>> GetAllTagsAsync()
    {
        return await _context.Tag.ToListAsync();
    }

    public async Task<IList<Tag>> GetAllTagsByNameAsync(IEnumerable<string> normalizedNames)
    {
        return await _context.Tag
            .Where(t => normalizedNames.Contains(t.NormalizedTitle))
            .ToListAsync();
    }

    public async Task<IList<TagDto>> GetAllTagDtosAsync(int userId)
    {
        var userRating = await _context.AppUser.GetUserAgeRestriction(userId);
        return await _context.Tag
            .AsNoTracking()
            .RestrictAgainstAgeRestriction(userRating)
            .OrderBy(t => t.NormalizedTitle)
            .ProjectTo<TagDto>(_mapper.ConfigurationProvider)
            .ToListAsync();
    }
}
