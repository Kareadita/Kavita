using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Comparators;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.SignalR;
using Microsoft.Extensions.Logging;

namespace API.Services;


public interface ISeriesService
{
    Task<SeriesDetailDto> GetSeriesDetail(int seriesId, int userId);
    Task<bool> UpdateSeriesMetadata(UpdateSeriesMetadataDto updateSeriesMetadataDto);
    Task<bool> UpdateRating(AppUser user, UpdateSeriesRatingDto updateSeriesRatingDto);
    Task<bool> DeleteMultipleSeries(IList<int> seriesIds);

}

public class SeriesService : ISeriesService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEventHub _eventHub;
    private readonly ITaskScheduler _taskScheduler;
    private readonly ILogger<SeriesService> _logger;

    public SeriesService(IUnitOfWork unitOfWork, IEventHub eventHub, ITaskScheduler taskScheduler, ILogger<SeriesService> logger)
    {
        _unitOfWork = unitOfWork;
        _eventHub = eventHub;
        _taskScheduler = taskScheduler;
        _logger = logger;
    }

    public async Task<bool> UpdateSeriesMetadata(UpdateSeriesMetadataDto updateSeriesMetadataDto)
    {
        try
        {
            var seriesId = updateSeriesMetadataDto.SeriesMetadata.SeriesId;
            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdAsync(seriesId);
            var allTags = (await _unitOfWork.CollectionTagRepository.GetAllTagsAsync()).ToList();
            if (series.Metadata == null)
            {
                series.Metadata = DbFactory.SeriesMetadata(updateSeriesMetadataDto.Tags
                    .Select(dto => DbFactory.CollectionTag(dto.Id, dto.Title, dto.Summary, dto.Promoted)).ToList());
            }
            else
            {

                series.Metadata.CollectionTags ??= new List<CollectionTag>();
                // TODO: Move this merging logic into a reusable code as it can be used for any Tag
                var newTags = new List<CollectionTag>();

                // I want a union of these 2 lists. Return only elements that are in both lists, but the list types are different
                var existingTags = series.Metadata.CollectionTags.ToList();
                foreach (var existing in existingTags)
                {
                    if (updateSeriesMetadataDto.Tags.SingleOrDefault(t => t.Id == existing.Id) == null)
                    {
                        // Remove tag
                        series.Metadata.CollectionTags.Remove(existing);
                    }
                }

                // At this point, all tags that aren't in dto have been removed.
                foreach (var tag in updateSeriesMetadataDto.Tags)
                {
                    var existingTag = allTags.SingleOrDefault(t => t.Title == tag.Title);
                    if (existingTag != null)
                    {
                        if (series.Metadata.CollectionTags.All(t => t.Title != tag.Title))
                        {
                            newTags.Add(existingTag);
                        }
                    }
                    else
                    {
                        // Add new tag
                        newTags.Add(DbFactory.CollectionTag(tag.Id, tag.Title, tag.Summary, tag.Promoted));
                    }
                }

                foreach (var tag in newTags)
                {
                    series.Metadata.CollectionTags.Add(tag);
                }
            }

            if (!_unitOfWork.HasChanges())
            {
                return true;
            }

            if (await _unitOfWork.CommitAsync())
            {
                foreach (var tag in updateSeriesMetadataDto.Tags)
                {
                    await _eventHub.SendMessageAsync(MessageFactory.SeriesAddedToCollection,
                        MessageFactory.SeriesAddedToCollectionEvent(tag.Id,
                            updateSeriesMetadataDto.SeriesMetadata.SeriesId), false);
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception when updating metadata");
            await _unitOfWork.RollbackAsync();
        }

        return false;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="user">User with Ratings includes</param>
    /// <param name="updateSeriesRatingDto"></param>
    /// <returns></returns>
    public async Task<bool> UpdateRating(AppUser user, UpdateSeriesRatingDto updateSeriesRatingDto)
    {
        if (user == null)
        {
            _logger.LogError("Cannot update rating of null user");
            return false;
        }

        var userRating =
            await _unitOfWork.UserRepository.GetUserRatingAsync(updateSeriesRatingDto.SeriesId, user.Id) ??
            new AppUserRating();
        try
        {
            userRating.Rating = Math.Clamp(updateSeriesRatingDto.UserRating, 0, 5);
            userRating.Review = updateSeriesRatingDto.UserReview;
            userRating.SeriesId = updateSeriesRatingDto.SeriesId;

            if (userRating.Id == 0)
            {
                user.Ratings ??= new List<AppUserRating>();
                user.Ratings.Add(userRating);
            }

            _unitOfWork.UserRepository.Update(user);

            if (!_unitOfWork.HasChanges() || await _unitOfWork.CommitAsync()) return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an exception saving rating");
        }

        await _unitOfWork.RollbackAsync();
        user.Ratings?.Remove(userRating);

        return false;
    }

    public async Task<bool> DeleteMultipleSeries(IList<int> seriesIds)
    {
        try
        {
            var chapterMappings =
                await _unitOfWork.SeriesRepository.GetChapterIdWithSeriesIdForSeriesAsync(seriesIds.ToArray());

            var allChapterIds = new List<int>();
            foreach (var mapping in chapterMappings)
            {
                allChapterIds.AddRange(mapping.Value);
            }

            var series = await _unitOfWork.SeriesRepository.GetSeriesByIdsAsync(seriesIds);
            _unitOfWork.SeriesRepository.Remove(series);

            if (!_unitOfWork.HasChanges() || !await _unitOfWork.CommitAsync()) return true;

            foreach (var s in series)
            {
                await _eventHub.SendMessageAsync(MessageFactory.SeriesRemoved,
                    MessageFactory.SeriesRemovedEvent(s.Id, s.Name, s.LibraryId), false);
            }

            await _unitOfWork.AppUserProgressRepository.CleanupAbandonedChapters();
            await _unitOfWork.CollectionTagRepository.RemoveTagsWithoutSeries();
            _taskScheduler.CleanupChapters(allChapterIds.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "There was an issue when trying to delete multiple series");
            return false;
        }

        return true;
    }

    /// <summary>
    /// This generates all the arrays needed by the Series Detail page in the UI. It is a specialized API for the unique layout constraints.
    /// </summary>
    /// <param name="seriesId"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task<SeriesDetailDto> GetSeriesDetail(int seriesId, int userId)
    {
        var series = await _unitOfWork.SeriesRepository.GetSeriesDtoByIdAsync(seriesId, userId);

        var libraryType = await _unitOfWork.LibraryRepository.GetLibraryTypeAsync(series.LibraryId);
        var volumes = (await _unitOfWork.VolumeRepository.GetVolumesDtoAsync(seriesId, userId)).OrderBy(v => float.Parse(v.Name)).ToList();
        var chapters = volumes.SelectMany(v => v.Chapters).ToList();

        // For books, the Name of the Volume is remapped to the actual name of the book, rather than Volume number.
        if (libraryType == LibraryType.Book)
        {
            foreach (var volume in volumes)
            {
                var firstChapter = volume.Chapters.First();
                if (!string.IsNullOrEmpty(firstChapter.TitleName)) volume.Name += $" - {firstChapter.TitleName}";
            }
        }


        var specials = new List<ChapterDto>();
        foreach (var chapter in chapters.Where(c => c.IsSpecial))
        {
            chapter.Title = Parser.Parser.CleanSpecialTitle(chapter.Title);
            specials.Add(chapter);
        }
        return new SeriesDetailDto()
        {
            Specials = specials,
            // Don't show chapter 0 (aka single volume chapters) in the Chapters tab or books that are just single numbers (they show as volumes)
            Chapters = chapters.Where(ShouldIncludeChapter)
                .OrderBy(c => float.Parse(c.Number), new ChapterSortComparer()),
            Volumes = volumes,
            StorylineChapters = volumes.Where(v => v.Number == 0).SelectMany(v => v.Chapters).OrderBy(c => float.Parse(c.Number), new ChapterSortComparer())

        };
    }

    /// <summary>
    /// Should we show the given chapter on the UI. We only show non-specials and non-zero chapters.
    /// </summary>
    /// <param name="c"></param>
    /// <returns></returns>
    private static bool ShouldIncludeChapter(ChapterDto c)
    {
        return !c.IsSpecial && !c.Number.Equals(Parser.Parser.DefaultChapter);
    }
}
