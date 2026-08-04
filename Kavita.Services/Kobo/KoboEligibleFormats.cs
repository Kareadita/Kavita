using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;

namespace Kavita.Services.Kobo;

/// <summary>
/// Format eligibility for Kobo sync: native EPUB or CBZ/CBR archives.
/// </summary>
public static class KoboEligibleFormats
{
    /// <summary>
    /// EF-translatable predicate: file is a native EPUB or convertible CBZ/CBR archive.
    /// </summary>
    public static Expression<Func<MangaFile, bool>> FileIsEligible { get; } = f =>
        f.Format == MangaFormat.Epub
        || (f.Format == MangaFormat.Archive && (
            f.Extension == ".cbz" || f.Extension == ".cbr"
            || f.FilePath.EndsWith(".cbz") || f.FilePath.EndsWith(".CBZ")
            || f.FilePath.EndsWith(".cbr") || f.FilePath.EndsWith(".CBR")));

    /// <summary>
    /// EF-translatable predicate: chapter has at least one eligible file.
    /// </summary>
    public static Expression<Func<Chapter, bool>> ChapterHasEligibleFile { get; } = c =>
        c.Files.Any(f => f.Format == MangaFormat.Epub
                         || (f.Format == MangaFormat.Archive && (
                             f.Extension == ".cbz" || f.Extension == ".cbr"
                             || f.FilePath.EndsWith(".cbz") || f.FilePath.EndsWith(".CBZ")
                             || f.FilePath.EndsWith(".cbr") || f.FilePath.EndsWith(".CBR"))));

    public static bool MatchesFile(MangaFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Format == MangaFormat.Epub) return true;
        return IsConvertibleArchive(file);
    }

    public static bool IsConvertibleArchive(MangaFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Format != MangaFormat.Archive) return false;
        if (!string.IsNullOrEmpty(file.Extension))
        {
            var ext = file.Extension.StartsWith('.') ? file.Extension : "." + file.Extension;
            if (ext.Equals(".cbz", StringComparison.OrdinalIgnoreCase) ||
                ext.Equals(".cbr", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return file.FilePath.EndsWith(".cbz", StringComparison.OrdinalIgnoreCase)
               || file.FilePath.EndsWith(".cbr", StringComparison.OrdinalIgnoreCase);
    }

    public static MangaFile? PreferNativeEpub(IEnumerable<MangaFile> files) =>
        files.FirstOrDefault(f => f.Format == MangaFormat.Epub);

    public static MangaFile? PreferConvertibleArchive(IEnumerable<MangaFile> files) =>
        files.FirstOrDefault(IsConvertibleArchive);
}
