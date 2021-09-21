using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using API.Helpers;
using API.Services;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    /// <summary>
    /// A data structure to migrate Cover Images from byte[] to files.
    /// </summary>
    internal class CoverMigration
    {
        public string Id { get; set; }
        public byte[] CoverImage { get; set; }
        public string ParentId { get; set; }
    }

    /// <summary>
    /// In v0.4.6, Cover Images were migrated from byte[] in the DB to external files. This migration handles that work.
    /// </summary>
    public static class MigrateCoverImages
    {
        /// <summary>
        /// Run first. Will extract byte[]s from DB and write them to the cover directory.
        /// </summary>
        public static void ExtractToImages(DbContext context)
        {
                Console.WriteLine("Migrating Cover Images to disk. Expect delay.");
                DirectoryService.ExistOrCreate(DirectoryService.CoverImageDirectory);

                Console.WriteLine("Extracting cover images for Series");
                var lockedSeries = SqlHelper.RawSqlQuery(context, "Select Id, CoverImage From Series Where CoverImage IS NOT NULL", x =>
                    new CoverMigration()
                    {
                        Id = x[0] + string.Empty,
                        CoverImage = (byte[]) x[1],
                        ParentId = "0"
                    });
                foreach (var series in lockedSeries)
                {
                    if (series.CoverImage == null || !series.CoverImage.Any()) continue;
                    if (File.Exists(Path.Join(DirectoryService.CoverImageDirectory,
                        $"{ImageService.GetSeriesFormat(int.Parse(series.Id))}.png"))) continue;

                    try
                    {
                        var stream = new MemoryStream(series.CoverImage);
                        stream.Position = 0;
                        ImageService.WriteCoverThumbnail(stream, ImageService.GetSeriesFormat(int.Parse(series.Id)));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }

                Console.WriteLine("Extracting cover images for Chapters");
                var chapters = SqlHelper.RawSqlQuery(context, "Select Id, CoverImage, VolumeId From Chapter Where CoverImage IS NOT NULL;", x =>
                    new CoverMigration()
                    {
                        Id = x[0] + string.Empty,
                        CoverImage = (byte[]) x[1],
                        ParentId = x[2] + string.Empty
                    });
                foreach (var chapter in chapters)
                {
                    if (chapter.CoverImage == null || !chapter.CoverImage.Any()) continue;
                    if (File.Exists(Path.Join(DirectoryService.CoverImageDirectory,
                        $"{ImageService.GetChapterFormat(int.Parse(chapter.Id), int.Parse(chapter.ParentId))}.png"))) continue;

                    try
                    {
                        var stream = new MemoryStream(chapter.CoverImage);
                        stream.Position = 0;
                        ImageService.WriteCoverThumbnail(stream, $"{ImageService.GetChapterFormat(int.Parse(chapter.Id), int.Parse(chapter.ParentId))}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }

                Console.WriteLine("Extracting cover images for Collection Tags");
                var tags = SqlHelper.RawSqlQuery(context, "Select Id, CoverImage From CollectionTag Where CoverImage IS NOT NULL;", x =>
                    new CoverMigration()
                    {
                        Id = x[0] + string.Empty,
                        CoverImage = (byte[]) x[1] ,
                        ParentId = "0"
                    });
                foreach (var tag in tags)
                {
                    if (tag.CoverImage == null || !tag.CoverImage.Any()) continue;
                    if (File.Exists(Path.Join(DirectoryService.CoverImageDirectory,
                        $"{ImageService.GetCollectionTagFormat(int.Parse(tag.Id))}.png"))) continue;
                    try
                    {
                        var stream = new MemoryStream(tag.CoverImage);
                        stream.Position = 0;
                        ImageService.WriteCoverThumbnail(stream, $"{ImageService.GetCollectionTagFormat(int.Parse(tag.Id))}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
        }

        /// <summary>
        /// Run after <see cref="ExtractToImages"/>. Will update the DB with names of files that were extracted.
        /// </summary>
        /// <param name="context"></param>
        public static async Task UpdateDatabaseWithImages(DataContext context)
        {
            Console.WriteLine("Updating Series entities");
            var lockedSeries = await context.Series.Where(s => s.CoverImageLocked).ToListAsync();
            foreach (var series in lockedSeries)
            {
                if (!File.Exists(Path.Join(DirectoryService.CoverImageDirectory,
                    $"{ImageService.GetSeriesFormat(series.Id)}.png"))) continue;
                series.CoverImage = $"{ImageService.GetSeriesFormat(series.Id)}.png";
            }

            await context.SaveChangesAsync();

            Console.WriteLine("Updating Chapter entities");
            var chapters = await context.Chapter.ToListAsync();
            foreach (var chapter in chapters)
            {
                if (File.Exists(Path.Join(DirectoryService.CoverImageDirectory,
                    $"{ImageService.GetChapterFormat(chapter.Id, chapter.VolumeId)}.png")))
                {
                    chapter.CoverImage = $"{ImageService.GetChapterFormat(chapter.Id, chapter.VolumeId)}.png";
                }

            }

            await context.SaveChangesAsync();

            Console.WriteLine("Updating Collection Tag entities");
            var tags = await context.CollectionTag.ToListAsync();
            foreach (var tag in tags)
            {
                if (File.Exists(Path.Join(DirectoryService.CoverImageDirectory,
                    $"{ImageService.GetCollectionTagFormat(tag.Id)}.png")))
                {
                    tag.CoverImage = $"{ImageService.GetCollectionTagFormat(tag.Id)}.png";
                }

            }

            await context.SaveChangesAsync();
            Console.WriteLine("Cover Image Migration completed");
        }

    }
}
