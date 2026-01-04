using System.Collections.Generic;
using System.Linq;
using API.DTOs.Reader;

namespace API.Helpers;
#nullable enable

public static class BookChapterItemHelper
{
    /// <summary>
    /// For a given page, finds all toc items that match the page number.
    /// Returns flattened list to allow for best decision-making.
    /// </summary>
    /// <param name="toc">The table of contents collection</param>
    /// <param name="pageNum">Page number to search for</param>
    /// <returns>Flattened list of all TOC items matching the page</returns>
    public static IList<BookChapterItem> GetTocForPage(ICollection<BookChapterItem> toc, int pageNum)
    {
        var flattenedToc = FlattenToc(toc);
        return flattenedToc.Where(item => item.Page == pageNum).ToList();
    }

    /// <summary>
    /// Flattens the hierarchical table of contents into a single list.
    /// Preserves all items regardless of nesting level.
    /// </summary>
    /// <param name="toc">The hierarchical table of contents</param>
    /// <returns>Flattened list of all TOC items</returns>
    public static IList<BookChapterItem> FlattenToc(ICollection<BookChapterItem> toc)
    {
        var result = new List<BookChapterItem>();

        foreach (var item in toc)
        {
            result.Add(item);

            if (item.Children?.Any() == true)
            {
                var childItems = FlattenToc(item.Children);
                result.AddRange(childItems);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the most specific (deepest nested) TOC item for a given page.
    /// Useful when you want the most granular chapter/section title.
    /// </summary>
    /// <param name="toc">The table of contents collection</param>
    /// <param name="pageNum">Page number to search for</param>
    /// <returns>The deepest nested TOC item for the page, or null if none found</returns>
    public static BookChapterItem? GetMostSpecificTocForPage(ICollection<BookChapterItem> toc, int pageNum)
    {
        var (item, _) =  GetTocItemsWithDepth(toc, pageNum, 0)
            .OrderByDescending(x => x.depth)
            .FirstOrDefault();
        return item;
    }

    /// <summary>
    /// Helper method that tracks depth while flattening, useful for determining hierarchy level.
    /// </summary>
    /// <param name="toc">Table of contents collection</param>
    /// <param name="pageNum">Page number to filter by</param>
    /// <param name="currentDepth">Current nesting depth</param>
    /// <returns>Items with their depth information</returns>
    private static IEnumerable<(BookChapterItem item, int depth)> GetTocItemsWithDepth(
        ICollection<BookChapterItem> toc, int pageNum, int currentDepth)
    {
        foreach (var item in toc)
        {
            if (item.Page == pageNum)
            {
                yield return (item, currentDepth);
            }

            if (item.Children?.Any() != true) continue;
            foreach (var childResult in GetTocItemsWithDepth(item.Children, pageNum, currentDepth + 1))
            {
                yield return childResult;
            }
        }
    }
}
