using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Kavita.Common.Helpers;

#nullable enable

public static class HtmlHelper
{
    private const int BodyTextLimit = 175;

    public static string? GetCharacters(string? body)
    {
        if (string.IsNullOrEmpty(body)) return body;

        var doc = new HtmlDocument();
        doc.LoadHtml(body);

        var textNodes = doc.DocumentNode.SelectNodes("//text()[not(parent::script)]");
        if (textNodes == null) return string.Empty;

        var plainText =  string.Join(" ", textNodes
            .Select(node => node.InnerText)
            .Where(s => !s.Equals("\n")));

        // Clean any leftover Markdown out
        plainText = Regex.Replace(plainText, @"\*\*(.*?)\*\*", "$1"); // Bold with **
        plainText = Regex.Replace(plainText, @"_(.*?)_", "$1"); // Italic with _
        plainText = Regex.Replace(plainText, @"\[(.*?)\]\((.*?)\)", "$1"); // Links [text](url)
        plainText = Regex.Replace(plainText, @"[_*\[\]~]", string.Empty);
        plainText = Regex.Replace(plainText, @"img\d*\((.*?)\)", string.Empty);
        plainText = Regex.Replace(plainText, @"~~~(.*?)~~~", "$1");
        plainText = Regex.Replace(plainText, @"\+{3}(.*?)\+{3}", "$1");
        plainText = Regex.Replace(plainText, @"~~(.*?)~~", "$1");
        plainText = Regex.Replace(plainText, @"__(.*?)__", "$1");
        plainText = Regex.Replace(plainText, @"#\s(.*?)", "$1");


        // Just strip symbols
        plainText = Regex.Replace(plainText, @"[_*\[\]~]", string.Empty);
        plainText = Regex.Replace(plainText, @"img\d*\((.*?)\)", string.Empty);
        plainText = Regex.Replace(plainText, @"~~~", string.Empty);
        plainText = Regex.Replace(plainText, @"\+", string.Empty);
        plainText = Regex.Replace(plainText, @"~~", string.Empty);
        plainText = Regex.Replace(plainText, @"__", string.Empty);

        // Take the first BodyTextLimit characters
        plainText = plainText.Length > BodyTextLimit ? plainText.Substring(0, BodyTextLimit) : plainText;

        return plainText + "…";
    }
}
