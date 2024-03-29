using System;
using System.IO;
using API.Entities;
using API.Entities.Enums;
using API.Services.Tasks.Scanner.Parser;

namespace API.Helpers.Builders;

public class MangaFileBuilder : IEntityBuilder<MangaFile>
{
    private readonly MangaFile _mangaFile;
    public MangaFile Build() => _mangaFile;

    public MangaFileBuilder(string filePath, MangaFormat format, int pages = 0)
    {
        _mangaFile = new MangaFile()
        {
            FilePath = Parser.NormalizePath(filePath),
            Format = format,
            Pages = pages,
            LastModified = File.GetLastWriteTime(filePath),
            LastModifiedUtc = File.GetLastWriteTimeUtc(filePath),
            FileName = Parser.RemoveExtensionIfSupported(filePath)
        };
    }

    public MangaFileBuilder WithFormat(MangaFormat format)
    {
        _mangaFile.Format = format;
        return this;
    }

    public MangaFileBuilder WithPages(int pages)
    {
        _mangaFile.Pages = Math.Max(pages, 0);
        return this;
    }

    public MangaFileBuilder WithExtension(string extension)
    {
        _mangaFile.Extension = extension.ToLowerInvariant();
        return this;
    }

    public MangaFileBuilder WithBytes(long bytes)
    {
        _mangaFile.Bytes = Math.Max(0, bytes);
        return this;
    }

    public MangaFileBuilder WithLastModified(DateTime dateTime)
    {
        _mangaFile.LastModified = dateTime;
        _mangaFile.LastModifiedUtc = dateTime.ToUniversalTime();
        return this;
    }

    public MangaFileBuilder WithId(int id)
    {
        _mangaFile.Id = Math.Max(id, 0);
        return this;
    }
}
