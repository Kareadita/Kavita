using System;
using System.Collections.Generic;
using System.Globalization;
using Kavita.Common.Extensions;
using Kavita.Models.Entities;
using Kavita.Models.Entities.Enums;
using Kavita.Models.Parser;

namespace Kavita.Services.Extensions;

public static class ChapterExtensions
{
    extension(Chapter chapter)
    {
        public void UpdateFrom(ParserInfo info)
        {
            chapter.Files ??= new List<MangaFile>();
            chapter.IsSpecial = info.IsSpecialInfo();
            if (chapter.IsSpecial)
            {
                chapter.Number = Scanner.Parser.DefaultChapter;
                chapter.MinNumber = Scanner.Parser.DefaultChapterNumber;
                chapter.MaxNumber = Scanner.Parser.DefaultChapterNumber;
            }
            chapter.Title = (chapter.IsSpecial && info.Format is MangaFormat.Epub or MangaFormat.Pdf)
                ? info.Title
                : Scanner.Parser.RemoveExtensionIfSupported(chapter.Range);

            var specialTreatment = info.IsSpecialInfo();
            chapter.Range = specialTreatment ? info.Filename : info.Chapters;
        }

        /// <summary>
        /// Returns the Chapter Number. If the chapter is a range, returns that, formatted.
        /// </summary>
        /// <returns></returns>
        public string GetNumberTitle()
        {
            try
            {
                if (chapter.MinNumber.Is(chapter.MaxNumber))
                {
                    if (chapter.MinNumber.Is(Scanner.Parser.DefaultChapterNumber) && chapter.IsSpecial)
                    {
                        return Scanner.Parser.RemoveExtensionIfSupported(chapter.Title) ?? string.Empty;
                    }

                    if (chapter.MinNumber.Is(0f) && !float.TryParse(chapter.Range, CultureInfo.InvariantCulture, out _))
                    {
                        return $"{chapter.Range.ToString(CultureInfo.InvariantCulture)}";
                    }

                    return $"{chapter.MinNumber.ToString(CultureInfo.InvariantCulture)}";

                }

                return $"{chapter.MinNumber.ToString(CultureInfo.InvariantCulture)}-{chapter.MaxNumber.ToString(CultureInfo.InvariantCulture)}";
            }
            catch (Exception)
            {
                return chapter.MinNumber.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
