using System;
using System.Linq;
using HtmlAgilityPack;

namespace Kavita.Services.Helpers.Kobo;

public static class KoboPositionHelper
{
    private const string KoboSpanClassName = "koboSpan";
    private const string BookColumnsId = "book-columns";
    private const string BookInnerId = "book-inner";
    private const string ReaderBodyScope = "//body";

    /// <summary>
    /// Encodes an XPath as the best fitting KoboSpan
    /// </summary>
    /// <param name="readerDoc"></param>
    /// <param name="kepubDoc"></param>
    /// <param name="xPath"></param>
    /// <returns></returns>
    public static string Encode(HtmlDocument readerDoc, HtmlDocument kepubDoc, string xPath)
    {
        var element = AnnotationHelper.FindElementByXPath(readerDoc, AnnotationHelper.DescopeXpath(xPath));
        if (element == null) return string.Empty;

        var kepubElement = FindKepubCounterpart(kepubDoc, element);
        if (kepubElement == null) return string.Empty;

        return FindFirstKoboSpanId(kepubElement) ?? string.Empty;
    }

    private static HtmlNode? FindKepubCounterpart(HtmlDocument kepubDoc, HtmlNode element)
    {
        var kepubRoot = kepubDoc.GetElementbyId(BookInnerId) ?? kepubDoc.DocumentNode.SelectSingleNode(ReaderBodyScope);
        if (kepubRoot == null) return null;

        for (var candidate = element; candidate != null; candidate = candidate.ParentNode)
        {
            if (candidate.NodeType != HtmlNodeType.Element) continue;
            if (candidate.Name.Equals("body", StringComparison.OrdinalIgnoreCase)) break;

            if (!string.IsNullOrEmpty(candidate.Id) && !IsKepubWrapperId(candidate.Id))
            {
                var byId = kepubDoc.GetElementbyId(candidate.Id);
                if (byId != null) return byId;
            }

            var relativePath = GetBodyRelativeXPath(candidate, stripBookInner: false);
            if (relativePath == null) continue;

            var kepubElement = kepubRoot.SelectSingleNode("." + relativePath);
            if (kepubElement != null && TextMatches(candidate, kepubElement))
            {
                return kepubElement;
            }
        }

        return null;
    }

    private static string? FindFirstKoboSpanId(HtmlNode node)
    {
        if (node.HasClass(KoboSpanClassName) && !string.IsNullOrEmpty(node.Id)) return node.Id;

        return node.ChildNodes
            .FirstOrDefault(child => child.HasClass(KoboSpanClassName) && !string.IsNullOrEmpty(child.Id))?
            .Id ?? string.Empty;
    }

    /// <summary>
    /// Decodes a KoboSpan to the best fitting XPath (Moves upwards to the non KoboSpan)
    /// </summary>
    /// <param name="kepubDoc"></param>
    /// <param name="readerDoc"></param>
    /// <param name="koboSpan"></param>
    /// <returns></returns>
    public static string Decode(HtmlDocument kepubDoc, HtmlDocument? readerDoc, string koboSpan)
    {
        var element = kepubDoc.GetElementbyId(koboSpan);
        if (element == null) return string.Empty;

        var block = ToBlockElement(element);
        if (block == null) return string.Empty;
        if (block.Name.Equals("body", StringComparison.OrdinalIgnoreCase)) return ReaderBodyScope;

        // Short track ids
        if (!string.IsNullOrEmpty(block.Id)
            && !IsKepubWrapperId(block.Id)
            && (readerDoc == null || readerDoc.GetElementbyId(block.Id) != null))
        {
            return $"id(\"{block.Id}\")";
        }

        // Kepubify wraps body content in #book-columns/#book-inner, which the reader's copy usually lacks
        var stripBookInner = readerDoc?.GetElementbyId(BookInnerId) == null;

        for (var candidate = block; candidate != null; candidate = candidate.ParentNode)
        {
            if (candidate.NodeType != HtmlNodeType.Element) continue;
            if (candidate.Name.Equals("body", StringComparison.OrdinalIgnoreCase)) break;
            if (IsKepubWrapper(candidate)) continue;

            var relativePath = GetBodyRelativeXPath(candidate, stripBookInner);
            if (relativePath == null) continue;

            if (readerDoc == null)
            {
                return ReaderBodyScope + relativePath;
            }

            var readerElement = readerDoc.DocumentNode.SelectSingleNode(ReaderBodyScope + relativePath);
            if (readerElement == null || !TextMatches(candidate, readerElement)) continue;

            var readerPath = GetBodyRelativeXPath(readerElement, stripBookInner: false);
            if (readerPath != null)
            {
                return ReaderBodyScope + readerPath;
            }
        }

        // One last try on finding an id to anchor on
        if (readerDoc != null)
        {
            for (var ancestor = block.ParentNode; ancestor != null; ancestor = ancestor.ParentNode)
            {
                if (ancestor.NodeType != HtmlNodeType.Element) continue;
                if (string.IsNullOrEmpty(ancestor.Id) || IsKepubWrapperId(ancestor.Id)) continue;
                if (readerDoc.GetElementbyId(ancestor.Id) != null)
                {
                    return $"id(\"{ancestor.Id}\")";
                }
            }
        }

        return string.Empty;
    }

    private static HtmlNode? ToBlockElement(HtmlNode element)
    {
        for (var node = element; node != null; node = node.ParentNode)
        {
            if (node.NodeType != HtmlNodeType.Element) continue;
            if (node.HasClass(KoboSpanClassName)) continue;
            if (IsKepubWrapper(node)) continue;
            if (AnnotationHelper.InlineTags.Contains(node.Name)) continue;

            return node;
        }

        return null;
    }

    private static string? GetBodyRelativeXPath(HtmlNode node, bool stripBookInner)
    {
        if (node.NodeType != HtmlNodeType.Element) return null;

        var root = stripBookInner ? FindAncestorWithId(node, BookInnerId) : null;
        root ??= FindAncestorByName(node, "body");
        if (root == null) return null;

        var nodePath = node.XPath;
        var rootPath = root.XPath;
        if (!nodePath.StartsWith(rootPath, StringComparison.Ordinal)) return null;

        return nodePath[rootPath.Length..];
    }

    private static HtmlNode? FindAncestorWithId(HtmlNode node, string id)
    {
        for (var current = node; current != null; current = current.ParentNode)
        {
            if (current.NodeType == HtmlNodeType.Element && current.Id == id) return current;
        }

        return null;
    }

    private static HtmlNode? FindAncestorByName(HtmlNode node, string name)
    {
        for (var current = node; current != null; current = current.ParentNode)
        {
            if (current.NodeType == HtmlNodeType.Element && current.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }
        }

        return null;
    }

    private static bool IsKepubWrapper(HtmlNode node)
    {
        return IsKepubWrapperId(node.Id);
    }

    private static bool IsKepubWrapperId(string? id)
    {
        return id is BookColumnsId or BookInnerId;
    }

    private static bool TextMatches(HtmlNode left, HtmlNode right)
    {
        var leftText = NormalizeText(left.InnerText);
        var rightText = NormalizeText(right.InnerText);

        if (leftText.Length == 0 || rightText.Length == 0) return true;

        return leftText.Contains(rightText, StringComparison.Ordinal)
               || rightText.Contains(leftText, StringComparison.Ordinal);
    }

    private static string NormalizeText(string text)
    {
        return string.Join(' ', HtmlEntity.DeEntitize(text).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
