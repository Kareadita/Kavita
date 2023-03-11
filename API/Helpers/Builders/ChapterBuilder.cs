using System.Collections.Generic;
using API.Entities;
using API.Entities.Enums;

namespace API.Helpers.Builders;

public class ChapterBuilder : IEntityBuilder<Chapter>
{
    private readonly Chapter _chapter;
    public Chapter Build() => _chapter;

    public ChapterBuilder(string number, string? range=null)
    {
        _chapter = new Chapter()
        {
            Range = string.IsNullOrEmpty(range) ? number : range,
            Title = string.IsNullOrEmpty(range) ? number : range,
            Number = API.Services.Tasks.Scanner.Parser.Parser.MinNumberFromRange(number) + string.Empty,
            Files = new List<MangaFile>(),
            Pages = 1
        };
    }

    public ChapterBuilder WithAgeRating(AgeRating rating)
    {
        _chapter.AgeRating = rating;
        return this;
    }

    public ChapterBuilder WithPages(int pages)
    {
        _chapter.Pages = pages;
        return this;
    }
    public ChapterBuilder WithCoverImage(string cover)
    {
        _chapter.CoverImage = cover;
        return this;
    }
    public ChapterBuilder WithIsSpecial(bool isSpecial)
    {
        _chapter.IsSpecial = isSpecial;
        return this;
    }
    public ChapterBuilder WithTitle(string title)
    {
        _chapter.Title = title;
        return this;
    }

    public ChapterBuilder WithFile(MangaFile file)
    {
        _chapter.Files ??= new List<MangaFile>();
        _chapter.Files.Add(file);
        return this;
    }
}
