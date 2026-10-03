using System.Collections;
using System.Reflection;
using Kavita.Models.Constants;
using Kavita.Server.Attributes;
using Kavita.Server.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Kavita.Server.Tests.Controllers;

public class EndpointAccessTests
{
    private static readonly Dictionary<string, Type> AccessAttributeByIdName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["seriesId"] = typeof(SeriesAccessAttribute),
        ["volumeId"] = typeof(VolumeAccessAttribute),
        ["chapterId"] = typeof(ChapterAccessAttribute),
        ["libraryId"] = typeof(LibraryAccessAttribute),
        ["personId"] = typeof(PersonAccessAttribute),
        ["readingListId"] = typeof(ReadingListAccessAttribute),
    };

    private static readonly HashSet<string> EntityIdPropertyNames = new(
        AccessAttributeByIdName.Keys.SelectMany(k => new[] { k, k + "s" }),
        StringComparer.OrdinalIgnoreCase);

    // Key: Controller.Action(paramTypes):param. Only add after reading the action and naming the check it performs
    private static readonly Dictionary<string, string> RouteQueryAllowlist = new()
    {
        ["ImageController.GetReadingListCoverImage(Int32):readingListId"] = "Body checks owner or promoted",
        ["MetadataController.GetAllAgeRatings(String):libraryIds"] = "Intersected with GetLibraryIdsForUserIdAsync",
        ["MetadataController.GetAllGenres(String, QueryContext):libraryIds"] = "Repo intersects ids with GetUserLibraries",
        ["MetadataController.GetAllLanguages(String):libraryIds"] = "Intersected with GetLibraryIdsForUserIdAsync",
        ["MetadataController.GetAllPeople(String):libraryIds"] = "Repo intersects ids with GetUserLibraries",
        ["MetadataController.GetAllPublicationStatus(String):libraryIds"] = "Intersected with GetLibraryIdsForUserIdAsync",
        ["MetadataController.GetAllTags(String):libraryIds"] = "Repo intersects ids with GetUserLibraries",
        ["OpdsController.GetPageStreamedImage(String, Int32, Int32, Int32, Int32, Int32, Boolean):libraryId"] = "SaveReadingProgress checks HasAccessToChapter and replaces the other ids with the chapter's own",
        ["OpdsController.GetPageStreamedImage(String, Int32, Int32, Int32, Int32, Int32, Boolean):seriesId"] = "SaveReadingProgress checks HasAccessToChapter and replaces the other ids with the chapter's own",
        ["OpdsController.GetPageStreamedImage(String, Int32, Int32, Int32, Int32, Int32, Boolean):volumeId"] = "SaveReadingProgress checks HasAccessToChapter and replaces the other ids with the chapter's own",
        ["OpdsController.GetReadingListItems(Int32, String, Int32):readingListId"] = "OpdsService throws reading-list-restricted when not visible to user",
        ["OpdsController.GetSeriesForLibrary(Int32, String, Int32):libraryId"] = "OpdsService checks GetLibrariesForUserIdAsync",
        ["PanelsController.GetProgress(Int32, String):chapterId"] = "Only returns the caller's own progress row",
        ["PersonController.GetRolesForPersonByName(Int32):personId"] = "Repo applies RestrictByLibrary and age restriction",
        ["ReaderController.GetNextChapter(Int32, Int32, Int32):volumeId"] = "Volume is looked up within the [SeriesAccess] series",
        ["ReaderController.GetPreviousChapter(Int32, Int32, Int32):volumeId"] = "Volume is looked up within the [SeriesAccess] series",
        ["ReaderController.ShouldPromptForChapterReRead(Int32, Int32, Int32):libraryId"] = "Only reads the library type for naming",
        ["ReaderController.ShouldPromptForSeriesReRead(Int32, Int32):libraryId"] = "Only reads the library type for naming",
        ["ReaderController.ShouldPromptForVolumeReRead(Int32, Int32, Int32):libraryId"] = "Only reads the library type for naming",
        ["ReadingListController.DeleteReadFromList(Int32):readingListId"] = "UserHasReadingListAccess in body",
        ["ReadingProfileController.GetProfileForSeries(Int32, Int32, Boolean, Nullable`1):libraryId"] = "Only selects among the caller's own reading profiles",
        ["ReadingProfileController.UpdateParentProfileForSeries(UserReadingProfileDto, Int32, Int32, Nullable`1):libraryId"] = "Only selects among the caller's own reading profiles",
        ["ReadingProfileController.UpdateReadingProfileForSeries(UserReadingProfileDto, Int32, Int32, Nullable`1):libraryId"] = "Only selects among the caller's own reading profiles",
        ["ReviewController.DeleteChapterReview(Int32):chapterId"] = "Only removes the caller's own rating rows",
        ["ReviewController.DeleteSeriesReview(Int32):seriesId"] = "Only removes the caller's own rating rows",
        ["ReviewController.GetMySeriesRatingAndReview(Int32):seriesId"] = "Only returns the caller's own rating",
        ["ScrobblingController.HasHold(Int32):seriesId"] = "Only reads the caller's own holds",
        ["ScrobblingController.RemoveHold(Int32):seriesId"] = "Only removes the caller's own holds",
        ["SeriesController.GetOnDeck(UserParams, Int32):libraryId"] = "Repo intersects with GetLibraryIdsForUser",
        ["SeriesController.RemoveFromOnDeck(Int32):seriesId"] = "Only writes the caller's own removal row, returns nothing",
        ["StatsController.GetReadingHistoryItemsForSeries(Int32, String, UserParams):seriesId"] = "Query filtered to the caller's own reading sessions",
        ["UsersController.HasLibraryAccess(Int32):libraryId"] = "Compares against the caller's own libraries",
        ["UsersController.HasReadingProgress(Int32):libraryId"] = "Only reads the library type, progress is the caller's own",
    };

    // Key: Controller.Action(paramTypes):Dto.Property. An entry means a human reviewed it, not that it is safe
    private static readonly Dictionary<string, string> DtoIdAllowlist = new()
    {
        ["AnnotationController.CreateAnnotation(AnnotationDto):AnnotationDto.ChapterId"] = "AnnotationService.CreateAnnotation HasAccessToChapter",
        ["AnnotationController.CreateAnnotation(AnnotationDto):AnnotationDto.LibraryId"] = "Ignored, derived from the chapter",
        ["AnnotationController.CreateAnnotation(AnnotationDto):AnnotationDto.SeriesId"] = "Ignored, derived from the chapter",
        ["AnnotationController.CreateAnnotation(AnnotationDto):AnnotationDto.VolumeId"] = "Ignored, derived from the chapter",
        ["AnnotationController.UpdateAnnotation(AnnotationDto):AnnotationDto.ChapterId"] = "Not read, only Id is used and the owner checked",
        ["AnnotationController.UpdateAnnotation(AnnotationDto):AnnotationDto.LibraryId"] = "Not read, only Id is used and the owner checked",
        ["AnnotationController.UpdateAnnotation(AnnotationDto):AnnotationDto.SeriesId"] = "Not read, only Id is used and the owner checked",
        ["AnnotationController.UpdateAnnotation(AnnotationDto):AnnotationDto.VolumeId"] = "Not read, only Id is used and the owner checked",
        ["CblController.CreateRemapRule(CreateRemapRuleDto):CreateRemapRuleDto.ChapterId"] = "IsAccessibleRemapTarget: series access, volume and chapter must belong to it",
        ["CblController.CreateRemapRule(CreateRemapRuleDto):CreateRemapRuleDto.SeriesId"] = "IsAccessibleRemapTarget: series access, volume and chapter must belong to it",
        ["CblController.CreateRemapRule(CreateRemapRuleDto):CreateRemapRuleDto.VolumeId"] = "IsAccessibleRemapTarget: series access, volume and chapter must belong to it",
        ["CblController.UpdateRemapRule(Int32, UpdateRemapRuleDto):UpdateRemapRuleDto.ChapterId"] = "IsAccessibleRemapTarget: series access, volume and chapter must belong to it",
        ["CblController.UpdateRemapRule(Int32, UpdateRemapRuleDto):UpdateRemapRuleDto.SeriesId"] = "IsAccessibleRemapTarget: series access, volume and chapter must belong to it",
        ["CblController.UpdateRemapRule(Int32, UpdateRemapRuleDto):UpdateRemapRuleDto.VolumeId"] = "IsAccessibleRemapTarget: series access, volume and chapter must belong to it",
        ["CollectionController.AddToMultipleSeries(CollectionTagBulkAddDto):CollectionTagBulkAddDto.SeriesIds"] = "HasAccessToSeries (all ids) in body",
        ["DeviceController.SendSeriesToDevice(SendSeriesToEmailDeviceDto):SendSeriesToEmailDeviceDto.SeriesId"] = "HasAccessToSeries in body",
        ["DeviceController.SendToDevice(SendToEmailDeviceDto):SendToEmailDeviceDto.ChapterIds"] = "DeviceService.SendTo HasAccessToAllChapters",
        ["DownloadController.GetBulkReadingListSize(BulkReadingListSizeRequest):BulkReadingListSizeRequest.ReadingListIds"] = "Repo GetFilesizesAsync is scoped by userId",
        ["DownloadController.GetBulkSeriesSize(BulkSeriesSizeRequest):BulkSeriesSizeRequest.SeriesIds"] = "Repo GetFilesizesAsync filters by user libraries and age",
        ["DownloadController.GetBulkVolumeSize(BulkVolumeSizeRequest):BulkVolumeSizeRequest.VolumeIds"] = "Repo GetFilesizesAsync filters by user libraries and age",
        ["DownloadController.GetChapterSizeInBulk(BulkChapterSizeRequest):BulkChapterSizeRequest.ChapterIds"] = "Repo GetFilesizesAsync filters by user libraries and age",
        ["KavitaPlusAuditController.GetMyActivity(KavitaPlusAuditFilterDto, UserParams):KavitaPlusAuditFilterDto.SeriesId"] = "Repo filters to the caller's own audit rows",
        ["PanelsController.SaveProgress(ProgressDto, String):ProgressDto.ChapterId"] = "HasAccessToChapter in body",
        ["PanelsController.SaveProgress(ProgressDto, String):ProgressDto.LibraryId"] = "SaveReadingProgress checks HasAccessToChapter and replaces the other ids with the chapter's own",
        ["PanelsController.SaveProgress(ProgressDto, String):ProgressDto.SeriesId"] = "SaveReadingProgress checks HasAccessToChapter and replaces the other ids with the chapter's own",
        ["PanelsController.SaveProgress(ProgressDto, String):ProgressDto.VolumeId"] = "SaveReadingProgress checks HasAccessToChapter and replaces the other ids with the chapter's own",
        ["RatingController.UpdateChapterRating(UpdateRatingDto):UpdateRatingDto.ChapterId"] = "Must belong to the HasAccessToSeries series",
        ["RatingController.UpdateChapterRating(UpdateRatingDto):UpdateRatingDto.SeriesId"] = "HasAccessToSeries in body",
        ["RatingController.UpdateSeriesRating(UpdateRatingDto):UpdateRatingDto.ChapterId"] = "Not read by UpdateSeriesRating",
        ["RatingController.UpdateSeriesRating(UpdateRatingDto):UpdateRatingDto.SeriesId"] = "HasAccessToSeries in body",
        ["ReaderController.BookmarkPage(BookmarkDto):BookmarkDto.ChapterId"] = "HasAccessToChapter in body",
        ["ReaderController.BookmarkPage(BookmarkDto):BookmarkDto.SeriesId"] = "Ignored, derived from the chapter",
        ["ReaderController.BookmarkPage(BookmarkDto):BookmarkDto.VolumeId"] = "Ignored, derived from the chapter",
        ["ReaderController.BulkRemoveBookmarks(BulkRemoveBookmarkForSeriesDto):BulkRemoveBookmarkForSeriesDto.SeriesIds"] = "Only removes the caller's own bookmarks",
        ["ReaderController.CreatePersonalToC(CreatePersonalToCDto):CreatePersonalToCDto.ChapterId"] = "HasAccessToChapter in body",
        ["ReaderController.CreatePersonalToC(CreatePersonalToCDto):CreatePersonalToCDto.LibraryId"] = "Ignored, derived from the chapter",
        ["ReaderController.CreatePersonalToC(CreatePersonalToCDto):CreatePersonalToCDto.SeriesId"] = "Ignored, derived from the chapter",
        ["ReaderController.CreatePersonalToC(CreatePersonalToCDto):CreatePersonalToCDto.VolumeId"] = "Ignored, derived from the chapter",
        ["ReaderController.MarkChapterAsRead(MarkChapterReadDto):MarkChapterReadDto.ChapterId"] = "HasAccessToChapter in body",
        ["ReaderController.MarkChapterAsRead(MarkChapterReadDto):MarkChapterReadDto.SeriesId"] = "Must equal the chapter's own series",
        ["ReaderController.MarkMultipleAsRead(MarkVolumesReadDto):MarkVolumesReadDto.ChapterIds"] = "Filtered to the HasAccessToSeries series",
        ["ReaderController.MarkMultipleAsRead(MarkVolumesReadDto):MarkVolumesReadDto.SeriesId"] = "HasAccessToSeries in body",
        ["ReaderController.MarkMultipleAsRead(MarkVolumesReadDto):MarkVolumesReadDto.VolumeIds"] = "Filtered to the HasAccessToSeries series",
        ["ReaderController.MarkMultipleAsUnread(MarkVolumesReadDto):MarkVolumesReadDto.ChapterIds"] = "Filtered to the HasAccessToSeries series",
        ["ReaderController.MarkMultipleAsUnread(MarkVolumesReadDto):MarkVolumesReadDto.SeriesId"] = "HasAccessToSeries in body",
        ["ReaderController.MarkMultipleAsUnread(MarkVolumesReadDto):MarkVolumesReadDto.VolumeIds"] = "Filtered to the HasAccessToSeries series",
        ["ReaderController.MarkMultipleSeriesAsRead(MarkMultipleSeriesAsReadDto):MarkMultipleSeriesAsReadDto.SeriesIds"] = "HasAccessToSeries (all ids) in body",
        ["ReaderController.MarkMultipleSeriesAsUnread(MarkMultipleSeriesAsReadDto):MarkMultipleSeriesAsReadDto.SeriesIds"] = "HasAccessToSeries (all ids) in body",
        ["ReaderController.MarkRead(MarkReadDto):MarkReadDto.SeriesId"] = "HasAccessToSeries in body",
        ["ReaderController.MarkUnread(MarkReadDto):MarkReadDto.SeriesId"] = "HasAccessToSeries in body",
        ["ReaderController.MarkVolumeAsRead(MarkVolumeReadDto):MarkVolumeReadDto.SeriesId"] = "HasAccessToVolume, series is taken from the volume",
        ["ReaderController.MarkVolumeAsRead(MarkVolumeReadDto):MarkVolumeReadDto.VolumeId"] = "HasAccessToVolume, series is taken from the volume",
        ["ReaderController.MarkVolumeAsUnread(MarkVolumeReadDto):MarkVolumeReadDto.SeriesId"] = "HasAccessToVolume, series is taken from the volume",
        ["ReaderController.MarkVolumeAsUnread(MarkVolumeReadDto):MarkVolumeReadDto.VolumeId"] = "HasAccessToVolume, series is taken from the volume",
        ["ReaderController.RemoveBookmarks(RemoveBookmarkForSeriesDto):RemoveBookmarkForSeriesDto.SeriesId"] = "Only removes the caller's own bookmarks",
        ["ReaderController.SaveProgress(ProgressDto):ProgressDto.ChapterId"] = "SaveReadingProgress checks HasAccessToChapter",
        ["ReaderController.SaveProgress(ProgressDto):ProgressDto.LibraryId"] = "SaveReadingProgress checks HasAccessToChapter and replaces the other ids with the chapter's own",
        ["ReaderController.SaveProgress(ProgressDto):ProgressDto.SeriesId"] = "SaveReadingProgress checks HasAccessToChapter and replaces the other ids with the chapter's own",
        ["ReaderController.SaveProgress(ProgressDto):ProgressDto.VolumeId"] = "SaveReadingProgress checks HasAccessToChapter and replaces the other ids with the chapter's own",
        ["ReaderController.UnBookmarkPage(BookmarkDto):BookmarkDto.ChapterId"] = "Only removes the caller's own bookmarks",
        ["ReaderController.UnBookmarkPage(BookmarkDto):BookmarkDto.SeriesId"] = "Only removes the caller's own bookmarks",
        ["ReaderController.UnBookmarkPage(BookmarkDto):BookmarkDto.VolumeId"] = "Only removes the caller's own bookmarks",
        ["ReadingListController.DeleteListItem(UpdateReadingListPosition):UpdateReadingListPosition.ReadingListId"] = "UserHasReadingListAccess in body",
        ["ReadingListController.DeleteMultipleReadingLists(DeleteReadingListsDto):DeleteReadingListsDto.ReadingListIds"] = "Only deletes from the caller's own lists",
        ["ReadingListController.PromoteMultipleReadingLists(PromoteReadingListsDto):PromoteReadingListsDto.ReadingListIds"] = "Skips lists where AppUserId != userId",
        ["ReadingListController.UpdateList(UpdateReadingListDto):UpdateReadingListDto.ReadingListId"] = "UserHasReadingListAccess in body",
        ["ReadingListController.UpdateListByChapter(UpdateReadingListByChapterDto):UpdateReadingListByChapterDto.ChapterId"] = "HasAccessToChapter in body",
        ["ReadingListController.UpdateListByChapter(UpdateReadingListByChapterDto):UpdateReadingListByChapterDto.ReadingListId"] = "UserHasReadingListAccess in body",
        ["ReadingListController.UpdateListByChapter(UpdateReadingListByChapterDto):UpdateReadingListByChapterDto.SeriesId"] = "Ignored, AddChaptersToReadingList reads it from each chapter",
        ["ReadingListController.UpdateListByMultiple(UpdateReadingListByMultipleDto):UpdateReadingListByMultipleDto.ChapterIds"] = "HasAccessToAllChapters in body",
        ["ReadingListController.UpdateListByMultiple(UpdateReadingListByMultipleDto):UpdateReadingListByMultipleDto.ReadingListId"] = "UserHasReadingListAccess in body",
        ["ReadingListController.UpdateListByMultiple(UpdateReadingListByMultipleDto):UpdateReadingListByMultipleDto.SeriesId"] = "Ignored, AddChaptersToReadingList reads it from each chapter",
        ["ReadingListController.UpdateListByMultiple(UpdateReadingListByMultipleDto):UpdateReadingListByMultipleDto.VolumeIds"] = "HasAccessToAllVolumes in body",
        ["ReadingListController.UpdateListByMultipleSeries(UpdateReadingListByMultipleSeriesDto):UpdateReadingListByMultipleSeriesDto.ReadingListId"] = "UserHasReadingListAccess in body",
        ["ReadingListController.UpdateListByMultipleSeries(UpdateReadingListByMultipleSeriesDto):UpdateReadingListByMultipleSeriesDto.SeriesIds"] = "HasAccessToSeries (all ids) in body",
        ["ReadingListController.UpdateListBySeries(UpdateReadingListBySeriesDto):UpdateReadingListBySeriesDto.ReadingListId"] = "UserHasReadingListAccess in body",
        ["ReadingListController.UpdateListBySeries(UpdateReadingListBySeriesDto):UpdateReadingListBySeriesDto.SeriesId"] = "HasAccessToSeries in body",
        ["ReadingListController.UpdateListByVolume(UpdateReadingListByVolumeDto):UpdateReadingListByVolumeDto.ReadingListId"] = "UserHasReadingListAccess in body",
        ["ReadingListController.UpdateListByVolume(UpdateReadingListByVolumeDto):UpdateReadingListByVolumeDto.SeriesId"] = "Ignored, AddChaptersToReadingList reads it from each chapter",
        ["ReadingListController.UpdateListByVolume(UpdateReadingListByVolumeDto):UpdateReadingListByVolumeDto.VolumeId"] = "HasAccessToVolume in body",
        ["ReadingListController.UpdateListItemPosition(UpdateReadingListPosition):UpdateReadingListPosition.ReadingListId"] = "UserHasReadingListAccess in body",
        ["ReadingProfileController.BulkAddReadingProfile(BulkSetSeriesProfiles):BulkSetSeriesProfiles.SeriesIds"] = "HasAccessToSeries (all ids) in body",
        ["ReadingProfileController.CreateReadingProfile(UserReadingProfileDto):UserReadingProfileDto.LibraryIds"] = "Not read, UpdateReaderProfileFields ignores id lists",
        ["ReadingProfileController.CreateReadingProfile(UserReadingProfileDto):UserReadingProfileDto.SeriesIds"] = "Not read, UpdateReaderProfileFields ignores id lists",
        ["ReadingProfileController.UpdateParentProfileForSeries(UserReadingProfileDto, Int32, Int32, Nullable`1):UserReadingProfileDto.LibraryIds"] = "Not read, UpdateReaderProfileFields ignores id lists",
        ["ReadingProfileController.UpdateParentProfileForSeries(UserReadingProfileDto, Int32, Int32, Nullable`1):UserReadingProfileDto.SeriesIds"] = "Not read, UpdateReaderProfileFields ignores id lists",
        ["ReadingProfileController.UpdateReadingProfile(UserReadingProfileDto):UserReadingProfileDto.LibraryIds"] = "Not read, UpdateReaderProfileFields ignores id lists",
        ["ReadingProfileController.UpdateReadingProfile(UserReadingProfileDto):UserReadingProfileDto.SeriesIds"] = "Not read, UpdateReaderProfileFields ignores id lists",
        ["ReadingProfileController.UpdateReadingProfileForSeries(UserReadingProfileDto, Int32, Int32, Nullable`1):UserReadingProfileDto.LibraryIds"] = "Not read, UpdateReaderProfileFields ignores id lists",
        ["ReadingProfileController.UpdateReadingProfileForSeries(UserReadingProfileDto, Int32, Int32, Nullable`1):UserReadingProfileDto.SeriesIds"] = "Not read, UpdateReaderProfileFields ignores id lists",
        ["ReviewController.UpdateChapterReview(UpdateUserReviewDto):UpdateUserReviewDto.ChapterId"] = "HasAccessToChapter in body",
        ["ReviewController.UpdateChapterReview(UpdateUserReviewDto):UpdateUserReviewDto.SeriesId"] = "Must equal the chapter's own series",
        ["ReviewController.UpdateSeriesReview(UpdateUserReviewDto):UpdateUserReviewDto.ChapterId"] = "Not read by UpdateSeriesReview",
        ["ReviewController.UpdateSeriesReview(UpdateUserReviewDto):UpdateUserReviewDto.SeriesId"] = "HasAccessToSeries in body",
        ["ScrobblingController.RetryScrobble(KavitaPlusAuditEntryDto):KavitaPlusAuditEntryDto.LibraryId"] = "Only Id is read, the entry is reloaded with GetEntryAsync and its owner checked",
        ["ScrobblingController.RetryScrobble(KavitaPlusAuditEntryDto):KavitaPlusAuditEntryDto.SeriesId"] = "Only Id is read, the entry is reloaded with GetEntryAsync and its owner checked",
        ["SeriesController.GetAllSeriesById(SeriesByIdsDto):SeriesByIdsDto.SeriesIds"] = "Repo filters by user libraries and age",
        ["StreamController.UpdateSideNavStream(SideNavStreamDto):SideNavStreamDto.LibraryId"] = "LibraryId is not read, only Id and Visible",
        ["WantToReadController.AddSeries(UpdateWantToReadDto):UpdateWantToReadDto.SeriesIds"] = "HasAccessToSeries (all ids) in body",
        ["WantToReadController.RemoveSeries(UpdateWantToReadDto):UpdateWantToReadDto.SeriesIds"] = "Only removes and scrobbles the caller's own want-to-read rows",
    };

    [Fact]
    public void RouteAndQueryEntityIds_HaveMatchingAccessAttribute()
    {
        var findings = new List<string>();

        foreach (var (controller, action) in NonAdminActions())
        {
            var accessKeys = AccessKeys(controller, action);

            foreach (var parameter in action.GetParameters())
            {
                var name = BoundName(parameter);
                if (!IsEntityIdName(name, out var attributeType, out var isCollection)) continue;
                if (parameter.GetCustomAttribute<FromBodyAttribute>() != null) continue;

                var key = $"{Signature(controller, action)}:{name}";
                if (isCollection || IsCollection(parameter.ParameterType))
                {
                    findings.Add($"{key} -> id collection, the access attribute only reads a single int");
                    continue;
                }

                if (!IsRouteOrQuery(parameter))
                {
                    findings.Add($"{key} -> bound from {BindingSourceName(parameter)}, the access attribute only reads route/query");
                    continue;
                }

                if (accessKeys.Any(k => k.Type == attributeType && string.Equals(k.Key, name, StringComparison.OrdinalIgnoreCase))) continue;

                findings.Add($"{key} -> missing [{attributeType.Name.Replace("Attribute", string.Empty)}]");
            }
        }

        AssertAgainstAllowlist(findings, RouteQueryAllowlist, nameof(RouteQueryAllowlist));
    }

    [Fact]
    public void DtoEntityIds_AreReviewed()
    {
        var findings = new List<string>();

        foreach (var (controller, action) in NonAdminActions())
        {
            foreach (var parameter in action.GetParameters().Where(p => IsComplexDto(p.ParameterType)))
            {
                findings.AddRange(parameter.ParameterType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => EntityIdPropertyNames.Contains(p.Name))
                    .Select(p => $"{Signature(controller, action)}:{parameter.ParameterType.Name}.{p.Name}"));
            }
        }

        AssertAgainstAllowlist(findings.Select(f => f + " -> entity ids in a DTO, confirm each id is access checked").ToList(),
            DtoIdAllowlist, nameof(DtoIdAllowlist));
    }

    private static void AssertAgainstAllowlist(List<string> findings, Dictionary<string, string> allowlist, string allowlistName)
    {
        var findingKeys = findings.Select(f => f.Split(" -> ")[0]).ToHashSet();

        var unreviewed = findings.Where(f => !allowlist.ContainsKey(f.Split(" -> ")[0])).ToList();
        var stale = allowlist.Keys.Where(k => !findingKeys.Contains(k)).ToList();
        var missingReason = allowlist.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ToList();

        var errors = new List<string>();
        if (unreviewed.Count > 0)
        {
            errors.Add($"Unreviewed endpoints ({unreviewed.Count}). Add the access attribute, or add to {allowlistName} with a reason:\n  " +
                       string.Join("\n  ", unreviewed.Order()));
        }

        if (stale.Count > 0)
        {
            errors.Add($"Stale {allowlistName} entries, remove them:\n  " + string.Join("\n  ", stale.Order()));
        }

        if (missingReason.Count > 0)
        {
            errors.Add($"{allowlistName} entries without a reason:\n  " + string.Join("\n  ", missingReason.Order()));
        }

        Assert.True(errors.Count == 0, string.Join("\n\n", errors));
    }

    private static IEnumerable<(Type Controller, MethodInfo Action)> NonAdminActions()
    {
        return typeof(BaseApiController).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(t))
            .Where(t => !IsAdminOnly(t))
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null)
                .Where(m => !IsAdminOnly(m))
                .Select(m => (t, m)));
    }

    private static bool IsAdminOnly(MemberInfo member)
    {
        return member.GetCustomAttributes<AuthorizeAttribute>(true).Any(a => a.Policy == PolicyGroups.AdminPolicy);
    }

    private static List<(Type Type, string Key)> AccessKeys(Type controller, MethodInfo action)
    {
        return action.GetCustomAttributesData()
            .Concat(controller.GetCustomAttributesData())
            .Where(a => typeof(AccessAttribute).IsAssignableFrom(a.AttributeType))
            .Select(a =>
            {
                var keyIndex = Array.FindIndex(a.Constructor.GetParameters(), p => p.Name!.EndsWith("Key"));

                return (a.AttributeType, (string) a.ConstructorArguments[keyIndex].Value!);
            })
            .ToList();
    }

    private static bool IsEntityIdName(string name, out Type attributeType, out bool isCollection)
    {
        isCollection = false;
        if (AccessAttributeByIdName.TryGetValue(name, out attributeType!)) return true;

        isCollection = name.EndsWith('s') && AccessAttributeByIdName.TryGetValue(name[..^1], out attributeType!);

        return isCollection;
    }

    private static string BoundName(ParameterInfo parameter)
    {
        var modelName = parameter.GetCustomAttributes()
            .OfType<IModelNameProvider>()
            .Select(a => a.Name)
            .FirstOrDefault(n => !string.IsNullOrEmpty(n));

        return modelName ?? parameter.Name!;
    }

    private static bool IsRouteOrQuery(ParameterInfo parameter)
    {
        var source = parameter.GetCustomAttributes().OfType<IBindingSourceMetadata>().FirstOrDefault()?.BindingSource;

        // Unannotated simple params bind from route or query under [ApiController]
        return source == null || source == BindingSource.Path || source == BindingSource.Query;
    }

    private static string BindingSourceName(ParameterInfo parameter)
    {
        return parameter.GetCustomAttributes().OfType<IBindingSourceMetadata>().FirstOrDefault()?.BindingSource?.DisplayName ?? "unknown";
    }

    private static bool IsCollection(Type type)
    {
        return type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);
    }

    private static bool IsComplexDto(Type type)
    {
        return type.IsClass && type != typeof(string) && !IsCollection(type) && !typeof(IFormFile).IsAssignableFrom(type) &&
               type.Namespace?.StartsWith("Kavita") == true;
    }

    private static string Signature(Type controller, MethodInfo action)
    {
        var paramTypes = string.Join(", ", action.GetParameters().Select(p => p.ParameterType.Name));

        return $"{controller.Name}.{action.Name}({paramTypes})";
    }
}
