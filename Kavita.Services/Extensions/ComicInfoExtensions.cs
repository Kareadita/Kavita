using Kavita.Models.Metadata;
using Nager.ArticleNumber;

namespace Kavita.Services.Extensions;

public static class ComicInfoExtensions
{

    extension(ComicInfo? info)
    {
        public void CleanComicInfo()
        {
            if (info == null) return;

            info.Series = info.Series.Trim();
            info.SeriesSort = info.SeriesSort.Trim();
            info.LocalizedSeries = info.LocalizedSeries.Trim();

            info.Writer = Scanner.Parser.CleanAuthor(info.Writer);
            info.Colorist = Scanner.Parser.CleanAuthor(info.Colorist);
            info.Editor = Scanner.Parser.CleanAuthor(info.Editor);
            info.Inker = Scanner.Parser.CleanAuthor(info.Inker);
            info.Letterer = Scanner.Parser.CleanAuthor(info.Letterer);
            info.Penciller = Scanner.Parser.CleanAuthor(info.Penciller);
            info.Publisher = Scanner.Parser.CleanAuthor(info.Publisher);
            info.Imprint = Scanner.Parser.CleanAuthor(info.Imprint);
            info.Characters = Scanner.Parser.CleanAuthor(info.Characters);
            info.Translator = Scanner.Parser.CleanAuthor(info.Translator);
            info.CoverArtist = Scanner.Parser.CleanAuthor(info.CoverArtist);
            info.Teams = Scanner.Parser.CleanAuthor(info.Teams);
            info.Locations = Scanner.Parser.CleanAuthor(info.Locations);

            // We need to convert GTIN to ISBN
            info.Isbn = ParseGtin(info.GTIN);

            if (!string.IsNullOrEmpty(info.Number))
            {
                info.Number = info.Number.Trim().Replace(",", "."); // Corrective measure for non English OSes
            }

            if (!string.IsNullOrEmpty(info.Volume))
            {
                info.Volume = info.Volume.Trim();
            }
        }
    }

    /// <summary>
    /// For a given GTIN, attempts to parse out an ISBN and set the Isbn property.
    /// </summary>
    /// <param name="gtin"></param>
    /// <returns></returns>
    public static string ParseGtin(string? gtin)
    {
        if (string.IsNullOrEmpty(gtin)) return string.Empty;


        // This is likely a valid ISBN
        if (gtin[0] == '0')
        {
            var offset = gtin[1] == '-'  ? 0 : 1;
            var potentialIsbn = gtin[offset..];
            if (ArticleNumberHelper.IsValidIsbn13(potentialIsbn))
            {
                return potentialIsbn;
            }
        }

        if (ArticleNumberHelper.IsValidIsbn10(gtin) || ArticleNumberHelper.IsValidIsbn13(gtin))
        {
            return gtin;
        }

        return string.Empty;
    }

}
