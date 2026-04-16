using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Kavita.API.Database;
using Kavita.API.Repositories;
using Kavita.API.Services;
using Kavita.API.Services.SignalR;
using Kavita.Common;
using Kavita.Models.DTOs.Annotations;
using Kavita.Models.DTOs.Reader;
using Kavita.Models.DTOs.SignalR;
using Kavita.Models.Entities.User;
using Kavita.Services.Helpers;
using Microsoft.Extensions.Logging;

namespace Kavita.Services;

public class AnnotationService(
    ILogger<AnnotationService> logger,
    IUnitOfWork unitOfWork,
    IBookService bookService,
    IEventHub eventHub)
    : IAnnotationService
{

    private static readonly JsonSerializerOptions ExportJsonSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Create a new Annotation for the user against a Chapter
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="dto"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException">Message is not localized</exception>
    public async Task<AnnotationDto> CreateAnnotation(int userId, AnnotationDto dto, CancellationToken ct = default)
    {
        try
        {
            if (dto.HighlightCount == 0 || string.IsNullOrWhiteSpace(dto.SelectedText))
            {
                throw new KavitaException("invalid-payload");
            }

            var chapter = await unitOfWork.ChapterRepository.GetChapterAsync(dto.ChapterId, ct: ct) ?? throw new KavitaException("chapter-doesnt-exist");
            var chapterTitle = string.Empty;

            try
            {
                var toc = await bookService.GenerateTableOfContents(chapter);
                var pageTocs = BookChapterItemHelper.GetTocForPage(toc, dto.PageNumber);
                if (pageTocs.Count > 0)
                {
                    chapterTitle = pageTocs[0].Title;
                }
            }
            catch (KavitaException)
            {
                /* Swallow */
            }

            var annotation = new AppUserAnnotation()
            {
                XPath = dto.XPath,
                EndingXPath = dto.EndingXPath,
                ChapterId = dto.ChapterId,
                SeriesId = dto.SeriesId,
                VolumeId = dto.VolumeId,
                LibraryId = dto.LibraryId,
                HighlightCount = dto.HighlightCount,
                SelectedText = dto.SelectedText,
                Comment = dto.Comment,
                CommentHtml = dto.CommentHtml,
                CommentPlainText = StripHtml(dto.CommentHtml),
                ContainsSpoiler = dto.ContainsSpoiler,
                PageNumber = dto.PageNumber,
                SelectedSlotIndex = dto.SelectedSlotIndex,
                AppUserId = userId,
                Context = dto.Context,
                ChapterTitle = chapterTitle
            };

            unitOfWork.AnnotationRepository.Attach(annotation);
            await unitOfWork.CommitAsync(ct);

            return (await unitOfWork.AnnotationRepository.GetAnnotationDto(annotation.Id, ct))!;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception when creating an annotation on {ChapterId} - Page {Page}", dto.ChapterId, dto.PageNumber);
            throw new KavitaException("annotation-failed-create");
        }
    }

    /// <summary>
    /// Update the modifiable fields (Spoiler, highlight slot, and comment) for an annotation
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="dto"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="KavitaException">Message is not localized</exception>
    public async Task<AnnotationDto> UpdateAnnotation(int userId, AnnotationDto dto, CancellationToken ct = default)
    {
        try
        {
            var annotation = await unitOfWork.AnnotationRepository.GetAnnotation(dto.Id, ct);
            if (annotation == null || annotation.AppUserId != userId) throw new KavitaException("denied");

            annotation.ContainsSpoiler = dto.ContainsSpoiler;
            annotation.SelectedSlotIndex = dto.SelectedSlotIndex;
            annotation.Comment = dto.Comment;
            annotation.CommentHtml = dto.CommentHtml;
            annotation.CommentPlainText = StripHtml(dto.CommentHtml);

            unitOfWork.AnnotationRepository.Update(annotation);

            if (!unitOfWork.HasChanges() || await unitOfWork.CommitAsync(ct))
            {
                dto = (await unitOfWork.AnnotationRepository.GetAnnotationDto(annotation.Id, ct))!;

                await eventHub.SendMessageToAsync(MessageFactory.AnnotationUpdate,
                    MessageFactory.AnnotationUpdateEvent(dto), userId, ct);

                return dto;
            }
        } catch (Exception ex)
        {
            logger.LogError(ex, "There was an exception updating Annotation for Chapter {ChapterId} - Page {PageNumber}",  dto.ChapterId, dto.PageNumber);
        }

        throw new KavitaException("generic-error");
    }

    public async Task<string> ExportAnnotations(int userId, IList<int>? annotationIds = null,
        CancellationToken ct = default)
    {
        try
        {
            // Get all annotations for the user with related data
            IList<FullAnnotationDto> annotations;
            if (annotationIds == null)
            {
                annotations = await unitOfWork.AnnotationRepository.GetFullAnnotationsByUserIdAsync(userId, ct);
            }
            else
            {
                annotations = await unitOfWork.AnnotationRepository.GetFullAnnotations(userId, annotationIds, ct);
            }

            var userIds = annotations.Select(a => a.UserId)
                .Distinct()
                .ToList();

            // Get users with preferences for highlight colors
            var users = (await unitOfWork.UserRepository.GetAllUsersAsync(AppUserIncludes.UserPreferences, false, ct))
                .Where(u => userIds.Contains(u.Id))
                .ToDictionary(u => u.Id, u => u);

            // Get settings for hostname
            var settings = await unitOfWork.SettingsRepository.GetSettingsDtoAsync(ct);
            var hostname = !string.IsNullOrWhiteSpace(settings.HostName) ? settings.HostName : $"http://localhost:{Configuration.Port}";

            // Group annotations by series, then by volume
            var exportData = annotations
                .GroupBy(a => new { a.SeriesId, a.SeriesName, a.LibraryId, a.LibraryName })
                .Select(seriesGroup => new
                {
                    series = new
                    {
                        id = seriesGroup.Key.SeriesId,
                        title = seriesGroup.Key.SeriesName,
                        libraryName = seriesGroup.Key.LibraryName,
                        libraryId = seriesGroup.Key.LibraryId
                    },
                    volumes = seriesGroup
                        .GroupBy(a => new { a.VolumeId, a.VolumeName })
                        .Select(volumeGroup => new
                        {
                            id = volumeGroup.Key.VolumeId,
                            title = volumeGroup.Key.VolumeName,
                            annotations = volumeGroup.Select(annotation =>
                            {
                                var user = users[annotation.UserId];
                                var highlightSlot = user.UserPreferences.BookReaderHighlightSlots
                                    .FirstOrDefault(slot => slot.SlotNumber == annotation.SelectedSlotIndex);

                                var slotColor = highlightSlot != null
                                    ? $"#{highlightSlot.Color.R:X2}{highlightSlot.Color.G:X2}{highlightSlot.Color.B:X2}"
                                    : "#000000";

                                var deepLink = $"{hostname}/library/{annotation.LibraryId}/series/{annotation.SeriesId}/book/{annotation.ChapterId}?incognitoMode=true&annotationId={annotation.Id}";

                                var obsidianTitle = $"{seriesGroup.Key.SeriesName} - {volumeGroup.Key.VolumeName}";
                                if (!string.IsNullOrWhiteSpace(annotation.ChapterTitle))
                                {
                                    obsidianTitle += $" - {annotation.ChapterTitle}";
                                }

                                return new
                                {
                                    id = annotation.Id,
                                    selectedText = annotation.SelectedText,
                                    comment = annotation.CommentHtml,
                                    context = annotation.Context,
                                    chapterTitle = annotation.ChapterTitle,
                                    pageNumber = annotation.PageNumber,
                                    slotColor,
                                    containsSpoiler = annotation.ContainsSpoiler,
                                    deepLink,
                                    createdUtc = annotation.CreatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                    lastModifiedUtc = annotation.LastModifiedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                                    obsidianTags = new[] { "#kavita", $"#{seriesGroup.Key.SeriesName.ToLowerInvariant().Replace(" ", "-")}", "#highlights" },
                                    obsidianTitle,
                                    obsidianBacklinks = new[] { $"[[{seriesGroup.Key.SeriesName} Series]]", $"[[{volumeGroup.Key.VolumeName}]]" }
                                };
                            }).ToArray(),
                        }).ToArray(),
                }).ToArray();

            // Serialize to JSON
            var json = JsonSerializer.Serialize(exportData, ExportJsonSerializerOptions);

            logger.LogInformation("Successfully exported {AnnotationCount} annotations for user {UserId}", annotations.Count, userId);

            return json;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export annotations for user {UserId}", userId);
            throw new KavitaException("annotation-export-failed");
        }
    }

    private string StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        try
        {
            var document = new HtmlDocument();
            document.LoadHtml(html);

            return document.DocumentNode.InnerText.Replace("&nbsp;", " ");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Invalid html, cannot parse plain text");
            return string.Empty;
        }
    }
}
