using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Kavita.Common.Helpers;

#nullable enable

public static partial class HtmlHelper
{
    private const int BodyTextLimit = 175;

    private static readonly HashSet<string> BlockElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "blockquote", "br", "dd", "div", "dl", "dt", "figcaption", "figure",
        "footer", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hr", "li", "main", "nav", "ol", "p", "pre",
        "section", "table", "td", "th", "tr", "ul",
    };

    private static readonly HashSet<string> SkippedElements = new(StringComparer.OrdinalIgnoreCase) { "script", "style" };

    /// <summary>
    /// Returns the text of an html fragment on one line, with entities decoded. Block elements separate words,
    /// inline elements do not: "&lt;p&gt;A&lt;/p&gt;&lt;p&gt;B&lt;/p&gt;" -> "A B", "&lt;b&gt;Tom&lt;/b&gt;'s" -> "Tom's"
    /// </summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var text = new StringBuilder();
        AppendText(doc.DocumentNode, text);

        return WhitespaceRegex().Replace(text.ToString(), " ").Trim();
    }

    private static void AppendText(HtmlNode node, StringBuilder text)
    {
        switch (node.NodeType)
        {
            case HtmlNodeType.Text:
                text.Append(HtmlEntity.DeEntitize(((HtmlTextNode) node).Text));
                return;
            case HtmlNodeType.Comment:
                return;
        }

        if (SkippedElements.Contains(node.Name)) return;

        var isBlock = BlockElements.Contains(node.Name);
        if (isBlock)
        {
            text.Append(' ');
        }

        foreach (var child in node.ChildNodes)
        {
            AppendText(child, text);
        }

        if (isBlock)
        {
            text.Append(' ');
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

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
