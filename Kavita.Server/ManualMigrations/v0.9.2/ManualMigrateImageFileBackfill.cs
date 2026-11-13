using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Kavita.API.Services;
using Kavita.Database;
using Kavita.Database.Extensions;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Entities.MetadataMatching;
using Kavita.Models.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kavita.Server.ManualMigrations.v0._9._2;

// Copies the obsolete CoverImage, color and bookmark FileName columns into ImageFile
#pragma warning disable CS0618

/// <summary>
/// v0.9.2 changed how image files are managed by Kavita to use a DB-approach instead, for better decision making and management capabilities.
/// This migration gives every image an <see cref="ImageFile"/> row and points its entity
/// at it. Chapters go first so a volume or series showing a chapter's file shares that chapter's row
/// </summary>
public class ManualMigrateImageFileBackfill(IDirectoryService directoryService) : ManualMigration
{
    private const int BatchSize = 1000;
    private static readonly ImageKind[] SeriesCoverKinds = [ImageKind.ChapterCover, ImageKind.VolumeCover, ImageKind.SeriesCover];

    protected override string MigrationName => nameof(ManualMigrateImageFileBackfill);

    // Links are set with ExecuteUpdate: saving tracked entities would stamp LastModified on every chapter and series
    protected override async Task ExecuteAsync(DataContext context, ILogger logger)
    {
        await LinkAsync(context, ImageKind.ChapterCover, SeriesCoverKinds,
            context.Chapter.AsNoTracking()
                .Where(c => c.CoverImageFileId == null && c.CoverImage != null && c.CoverImage != "")
                .Select(c => new { c.Id, c.CoverImage, c.PrimaryColor, c.SecondaryColor, c.CoverImageLocked, c.KPlusOverrides }),
            r => r.Id,
            r => new CoverRow(r.CoverImage!, r.PrimaryColor, r.SecondaryColor,
                OriginFromLock(r.CoverImageLocked, r.KPlusOverrides, MetadataSettingField.ChapterCovers)),
            (ids, kinds) => context.Chapter.Where(c => ids.Contains(c.Id)).ExecuteUpdateAsync(s => s.SetProperty(
                c => c.CoverImageFileId,
                c => context.ImageFile.Where(f => f.FileName == c.CoverImage && kinds.Contains(f.Kind))
                    .OrderBy(f => f.Kind).Select(f => (int?) f.Id).FirstOrDefault())));

        await LinkAsync(context, ImageKind.VolumeCover, SeriesCoverKinds,
            context.Volume.AsNoTracking()
                .Where(v => v.CoverImageFileId == null && v.CoverImage != null && v.CoverImage != "")
                .Select(v => new { v.Id, v.CoverImage, v.PrimaryColor, v.SecondaryColor, v.CoverImageLocked, v.KPlusOverrides }),
            r => r.Id,
            r => new CoverRow(r.CoverImage!, r.PrimaryColor, r.SecondaryColor,
                OriginFromLock(r.CoverImageLocked, r.KPlusOverrides, MetadataSettingField.VolumeCovers)),
            (ids, kinds) => context.Volume.Where(v => ids.Contains(v.Id)).ExecuteUpdateAsync(s => s.SetProperty(
                v => v.CoverImageFileId,
                v => context.ImageFile.Where(f => f.FileName == v.CoverImage && kinds.Contains(f.Kind))
                    .OrderBy(f => f.Kind).Select(f => (int?) f.Id).FirstOrDefault())));

        await LinkAsync(context, ImageKind.SeriesCover, SeriesCoverKinds,
            context.Series.AsNoTracking()
                .Where(s => s.CoverImageFileId == null && s.CoverImage != null && s.CoverImage != "")
                .Select(s => new { s.Id, s.CoverImage, s.PrimaryColor, s.SecondaryColor, s.CoverImageLocked, s.Metadata.KPlusOverrides }),
            r => r.Id,
            r => new CoverRow(r.CoverImage!, r.PrimaryColor, r.SecondaryColor,
                OriginFromLock(r.CoverImageLocked, r.KPlusOverrides, MetadataSettingField.Covers)),
            (ids, kinds) => context.Series.Where(s => ids.Contains(s.Id)).ExecuteUpdateAsync(u => u.SetProperty(
                s => s.CoverImageFileId,
                s => context.ImageFile.Where(f => f.FileName == s.CoverImage && kinds.Contains(f.Kind))
                    .OrderBy(f => f.Kind).Select(f => (int?) f.Id).FirstOrDefault())));

        await LinkAsync(context, ImageKind.PersonCover, [ImageKind.PersonCover],
            context.Person.AsNoTracking()
                .Where(p => p.CoverImageFileId == null && p.CoverImage != null && p.CoverImage != "")
                .Select(p => new { p.Id, p.CoverImage, p.PrimaryColor, p.SecondaryColor, p.CoverImageLocked, p.AniListId, p.HardcoverId }),
            r => r.Id,
            r => new CoverRow(r.CoverImage!, r.PrimaryColor, r.SecondaryColor,
                r.CoverImageLocked && (r.AniListId > 0 || !string.IsNullOrEmpty(r.HardcoverId)) ? ImageOrigin.KavitaPlus : ImageOrigin.Upload),
            (ids, kinds) => context.Person.Where(p => ids.Contains(p.Id)).ExecuteUpdateAsync(s => s.SetProperty(
                p => p.CoverImageFileId,
                p => context.ImageFile.Where(f => f.FileName == p.CoverImage && kinds.Contains(f.Kind))
                    .Select(f => (int?) f.Id).FirstOrDefault())));

        await LinkAsync(context, ImageKind.UserCover, [ImageKind.UserCover],
            context.AppUser.AsNoTracking()
                .Where(u => u.CoverImageFileId == null && u.CoverImage != null && u.CoverImage != "")
                .Select(u => new { u.Id, u.CoverImage, u.PrimaryColor, u.SecondaryColor }),
            r => r.Id,
            r => new CoverRow(r.CoverImage!, r.PrimaryColor, r.SecondaryColor, ImageOrigin.Legacy),
            (ids, kinds) => context.AppUser.Where(u => ids.Contains(u.Id)).ExecuteUpdateAsync(s => s.SetProperty(
                u => u.CoverImageFileId,
                u => context.ImageFile.Where(f => f.FileName == u.CoverImage && kinds.Contains(f.Kind))
                    .Select(f => (int?) f.Id).FirstOrDefault())));

        await LinkAsync(context, ImageKind.ReadingListCover, [ImageKind.ReadingListCover],
            context.ReadingList.AsNoTracking()
                .Where(l => l.CoverImageFileId == null && l.CoverImage != null && l.CoverImage != "")
                .Select(l => new { l.Id, l.CoverImage, l.PrimaryColor, l.SecondaryColor }),
            r => r.Id,
            r => new CoverRow(r.CoverImage!, r.PrimaryColor, r.SecondaryColor, ImageOrigin.Legacy),
            (ids, kinds) => context.ReadingList.Where(l => ids.Contains(l.Id)).ExecuteUpdateAsync(s => s.SetProperty(
                l => l.CoverImageFileId,
                l => context.ImageFile.Where(f => f.FileName == l.CoverImage && kinds.Contains(f.Kind))
                    .Select(f => (int?) f.Id).FirstOrDefault())));

        await LinkAsync(context, ImageKind.CollectionCover, [ImageKind.CollectionCover],
            context.AppUserCollection.AsNoTracking()
                .Where(c => c.CoverImageFileId == null && c.CoverImage != null && c.CoverImage != "")
                .Select(c => new { c.Id, c.CoverImage, c.PrimaryColor, c.SecondaryColor }),
            r => r.Id,
            r => new CoverRow(r.CoverImage!, r.PrimaryColor, r.SecondaryColor, ImageOrigin.Legacy),
            (ids, kinds) => context.AppUserCollection.Where(c => ids.Contains(c.Id)).ExecuteUpdateAsync(s => s.SetProperty(
                c => c.CoverImageFileId,
                c => context.ImageFile.Where(f => f.FileName == c.CoverImage && kinds.Contains(f.Kind))
                    .Select(f => (int?) f.Id).FirstOrDefault())));

        await LinkAsync(context, ImageKind.LibraryIcon, [ImageKind.LibraryIcon],
            context.Library.AsNoTracking()
                .Where(l => l.CoverImageFileId == null && l.CoverImage != null && l.CoverImage != "")
                .Select(l => new { l.Id, l.CoverImage, l.PrimaryColor, l.SecondaryColor }),
            r => r.Id,
            r => new CoverRow(r.CoverImage!, r.PrimaryColor, r.SecondaryColor, ImageOrigin.Legacy),
            (ids, kinds) => context.Library.Where(l => ids.Contains(l.Id)).ExecuteUpdateAsync(s => s.SetProperty(
                l => l.CoverImageFileId,
                l => context.ImageFile.Where(f => f.FileName == l.CoverImage && kinds.Contains(f.Kind))
                    .Select(f => (int?) f.Id).FirstOrDefault())));

        await LinkAsync(context, ImageKind.Bookmark, [ImageKind.Bookmark],
            context.AppUserBookmark.AsNoTracking()
                .Where(b => b.ImageFileId == null && b.FileName != null && b.FileName != "")
                .Select(b => new { b.Id, b.FileName }),
            r => r.Id,
            r => new CoverRow(r.FileName, null, null, ImageOrigin.Legacy),
            (ids, kinds) => context.AppUserBookmark.Where(b => ids.Contains(b.Id)).ExecuteUpdateAsync(s => s.SetProperty(
                b => b.ImageFileId,
                b => context.ImageFile.Where(f => f.FileName == b.FileName && kinds.Contains(f.Kind))
                    .Select(f => (int?) f.Id).FirstOrDefault())));

        await AddFolderAsync(context, ImageKind.Favicon, directoryService.FaviconDirectory, logger);
        await AddFolderAsync(context, ImageKind.Publisher, directoryService.PublisherDirectory, logger);

        var counts = await context.ImageFile.GroupBy(f => f.Kind)
            .Select(g => new { Kind = g.Key, Count = g.Count() })
            .ToListAsync();
        logger.LogInformation("Image file rows by kind: {Counts}",
            string.Join(", ", counts.Select(c => $"{c.Kind} {c.Count}")));
    }

    /// <summary>
    /// Pages over <paramref name="pending"/> (entities with a cover and no link), adds a row for each file name that
    /// has none among <paramref name="matchKinds"/>, then runs <paramref name="link"/> on the batch's ids
    /// </summary>
    private static async Task LinkAsync<TRow>(DataContext context, ImageKind kind, ImageKind[] matchKinds,
        IQueryable<TRow> pending, Expression<Func<TRow, int>> key, Func<TRow, CoverRow> toCover,
        Func<List<int>, ImageKind[], Task<int>> link)
    {
        var getKey = key.Compile();

        await foreach (var batch in pending.BatchByKeyAsync(key, BatchSize))
        {
            var covers = batch.Select(toCover).ToList();
            var names = covers.Select(c => c.FileName).Distinct().ToList();
            var known = (await context.ImageFile
                    .Where(f => names.Contains(f.FileName) && matchKinds.Contains(f.Kind))
                    .Select(f => f.FileName)
                    .ToListAsync())
                .ToHashSet();

            foreach (var cover in covers.Where(c => !known.Contains(c.FileName)).DistinctBy(c => c.FileName))
            {
                context.ImageFile.Add(new ImageFile
                {
                    Kind = kind,
                    FileName = cover.FileName,
                    Origin = cover.Origin,
                    PrimaryColor = string.IsNullOrEmpty(cover.PrimaryColor) ? null : cover.PrimaryColor,
                    SecondaryColor = string.IsNullOrEmpty(cover.SecondaryColor) ? null : cover.SecondaryColor,
                });
            }

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            await link(batch.Select(getKey).ToList(), matchKinds);
        }
    }

    private async Task AddFolderAsync(DataContext context, ImageKind kind, string directory, ILogger logger)
    {
        var fileSystem = directoryService.FileSystem;
        if (!fileSystem.Directory.Exists(directory)) return;

        var known = (await context.ImageFile.Where(f => f.Kind == kind).Select(f => f.FileName).ToListAsync()).ToHashSet();
        var added = 0;
        foreach (var path in fileSystem.Directory.EnumerateFiles(directory))
        {
            var fileName = Path.GetFileName(path);
            if (known.Contains(fileName)) continue;

            context.ImageFile.Add(new ImageFile
            {
                Kind = kind,
                FileName = fileName,
                Origin = ImageOrigin.Legacy,
                Key = Path.GetFileNameWithoutExtension(fileName),
            });
            added++;
        }

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        logger.LogInformation("Added {Count} {Kind} image rows from {Directory}", added, kind, directory);
    }

    private static ImageOrigin OriginFromLock(bool locked, IList<MetadataSettingField>? kPlusOverrides, MetadataSettingField coverField)
    {
        if (!locked) return ImageOrigin.Scan;
        return kPlusOverrides != null && kPlusOverrides.Contains(coverField) ? ImageOrigin.KavitaPlus : ImageOrigin.Upload;
    }

    private sealed record CoverRow(string FileName, string? PrimaryColor, string? SecondaryColor, ImageOrigin Origin);
}
