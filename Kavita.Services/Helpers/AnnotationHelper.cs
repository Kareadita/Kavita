using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Kavita.Models.DTOs.Reader;
using Serilog;

namespace Kavita.Services.Helpers;

public static partial class AnnotationHelper
{
    private const string UiXPathScope = "//BODY/DIV[1]"; // Div[1] is the div we inject reader contents into
    private const string HighlightTagName = "app-epub-highlight";
    private const string ElementTextSeparator = "\n\n";
    private const int MaxCharacterReferenceLength = 32;

    /// <summary>
    /// Used to break out of inline elements when selecting start- and end-elements.
    /// If we don't do this; <p><em>foo</em></p> will have em selected as start and <see cref="GetElementsInRange"/>
    /// fails to select the correct elements
    /// </summary>
    private static readonly HashSet<string> InlineTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "em", "strong", "i", "b", "span", "a", "cite", "sup", "sub", "small", "mark", "code", "kbd", "samp",
        "var", "q", "abbr", "dfn", "u", "s", "del", "ins", "strike", "font", "big", "tt", "nobr", "ruby",
        "rt", "rp", "bdi", "bdo", "time", "label",
        // The reader captures xpaths against a DOM that already contains injected highlights
        HighlightTagName
    };

    /// <summary>
    /// Tags to fully ignore while splitting annotations
    /// </summary>
    private static readonly HashSet<string> NoWrapTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style"
    };

    [GeneratedRegex("""^id\("([^"]+)"\)$""")]
    private static partial Regex IdXPathRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();


    /// <summary>
    /// Given an xpath that is scoped to the epub reader, transform it into a page-level xpath
    /// </summary>
    /// <param name="xpath"></param>
    /// <returns></returns>
    public static string DescopeXpath(string xpath)
    {
        return xpath.Replace(UiXPathScope, "//BODY").ToLowerInvariant();
    }

    public static void InjectSingleElementAnnotations(HtmlDocument doc, List<AnnotationDto> annotations)
    {
        var annotationsByElement = annotations
            .GroupBy(a => a.XPath)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (xpath, elementAnnotations) in annotationsByElement)
        {
            try
            {
                var elem = FindElementByXPath(doc, xpath);
                if (elem == null) continue;

                var originalText = GetTextForHighlighting(elem);
                var (decodedText, rawStarts) = DecodeWithMap(originalText);

                // Calculate positions and sort by start position
                var normalizedOriginalText = NormalizeWhitespace(decodedText);

                var sortedAnnotations = elementAnnotations
                    .Select(a => new
                    {
                        Annotation = a,
                        StartPos = normalizedOriginalText.IndexOf(NormalizeWhitespace(a.SelectedText), StringComparison.Ordinal)
                    })
                    .Where(a => a.StartPos >= 0)
                    .OrderBy(a => a.StartPos)
                    .ToList();

                foreach (var item in sortedAnnotations)
                {
                    var decodedStart = MapNormalizedPositionToText(decodedText, item.StartPos);
                    var decodedEnd = MapNormalizedPositionToText(decodedText, item.StartPos + item.Annotation.SelectedText.Length);

                    WrapTextRange(elem, rawStarts[decodedStart], rawStarts[decodedEnd], item.Annotation.Id, doc);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex, "Failed to inject annotation into element");
                /* Swallow */
                return;
            }
        }
    }

    public static void InjectMultiElementAnnotations(HtmlDocument doc, List<AnnotationDto> annotations)
    {
        foreach (var annotation in annotations)
        {
            try
            {
                var startXPath = DescopeXpath(annotation.XPath);
                var endXPath = DescopeXpath(annotation.EndingXPath);

                var startElement = NormalizeToBlockElement(FindElementByXPath(doc, startXPath));
                var endElement = NormalizeToBlockElement(FindElementByXPath(doc, endXPath));

                if (startElement == null || endElement == null) continue;

                // Get all elements between start and end (including start and end)
                var elementsInRange = GetElementsInRange(startElement, endElement);
                if (elementsInRange.Count == 0) continue;

                // Build full text to find our selection
                var fullText = string.Join(ElementTextSeparator, elementsInRange.Select(GetTextForHighlighting));
                var (decodedFullText, rawStarts) = DecodeWithMap(fullText);

                // Normalize both texts for comparison
                var normalizedFullText = NormalizeWhitespace(decodedFullText);
                var normalizedSelectedText = NormalizeWhitespace(annotation.SelectedText);

                var selectionStartPos = normalizedFullText.IndexOf(normalizedSelectedText, StringComparison.Ordinal);

                if (selectionStartPos == -1)
                {
                    Log.Logger.Debug("Failed to inject annotation {AnnotationId}, selected text not found", annotation.Id);
                    continue;
                }

                var selectionEndPos = selectionStartPos + normalizedSelectedText.Length;

                // Map positions back to elements using the original (non-normalized) text
                var elementTextMappings = BuildElementTextMappings(elementsInRange);

                // Convert normalized positions back to raw text positions, which is the space the mappings describe
                var originalSelectionStart = rawStarts[MapNormalizedPositionToText(decodedFullText, selectionStartPos)];
                var originalSelectionEnd = rawStarts[MapNormalizedPositionToText(decodedFullText, selectionEndPos)];

                // Process each element in the range
                for (var i = 0; i < elementsInRange.Count; i++)
                {
                    var mapping = elementTextMappings[i];

                    // Determine what part of this element should be highlighted
                    var highlightStart = Math.Max(originalSelectionStart - mapping.StartPos, 0);
                    var highlightEnd = Math.Min(originalSelectionEnd - mapping.StartPos, mapping.TextLength);

                    if (highlightEnd <= highlightStart) continue; // No highlight in this element

                    WrapTextRange(elementsInRange[i], highlightStart, highlightEnd, annotation.Id, doc);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex, "Failed to inject annotation {AnnotationId} into elements", annotation.Id);
                /* Swallow */
            }
        }
    }

    private static void WrapTextRange(HtmlNode root, int start, int end, int annotationId, HtmlDocument doc)
    {
        if (end <= start) return;

        foreach (var (node, runStart, runLength) in BuildTextRuns(root))
        {
            if (runStart >= end) break;

            var runEnd = runStart + runLength;
            if (runEnd <= start) continue;

            var parent = node.ParentNode;
            if (parent == null) continue;

            if (IsInsideHighlight(node)) continue;

            var text = GetText(node);
            var localStart = Math.Clamp(start - runStart, 0, runLength);
            var localEnd = Math.Clamp(end - runStart, 0, runLength);
            if (localEnd <= localStart) continue;

            if (localStart > 0)
            {
                parent.InsertBefore(doc.CreateTextNode(text[..localStart]), node);
            }

            var highlight = doc.CreateElement(HighlightTagName);
            highlight.SetAttributeValue("id", $"epub-highlight-{annotationId}");
            highlight.SetAttributeValue("data-annotation-id", annotationId.ToString());
            highlight.AppendChild(doc.CreateTextNode(text[localStart..localEnd]));
            parent.InsertBefore(highlight, node);

            if (localEnd < text.Length)
            {
                parent.InsertBefore(doc.CreateTextNode(text[localEnd..]), node);
            }

            parent.RemoveChild(node);
        }
    }

    private static List<(HtmlNode Node, int Start, int Length)> BuildTextRuns(HtmlNode root)
    {
        var runs = new List<(HtmlNode, int, int)>();
        var offset = 0;

        foreach (var node in root.DescendantsAndSelf())
        {
            if (node.NodeType != HtmlNodeType.Text) continue;
            if (IsInsideNoWrapElement(node, root)) continue;

            var text = GetText(node);
            if (text.Length == 0) continue;

            runs.Add((node, offset, text.Length));
            offset += text.Length;
        }

        return runs;
    }

    private static string GetTextForHighlighting(HtmlNode root)
    {
        return string.Concat(BuildTextRuns(root).Select(run => GetText(run.Node)));
    }

    private static string GetText(HtmlNode textNode)
    {
        return textNode is HtmlTextNode text ? text.Text : textNode.InnerText;
    }

    private static bool IsInsideNoWrapElement(HtmlNode node, HtmlNode root)
    {
        for (var parent = node.ParentNode; parent != null && parent != root; parent = parent.ParentNode)
        {
            if (parent.NodeType == HtmlNodeType.Element && NoWrapTags.Contains(parent.Name)) return true;
        }

        return false;
    }

    private static bool IsInsideHighlight(HtmlNode node)
    {
        for (var parent = node.ParentNode; parent != null; parent = parent.ParentNode)
        {
            if (parent.NodeType == HtmlNodeType.Element &&
                parent.Name.Equals(HighlightTagName, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /// <summary>
    /// <paramref name="raw"/> with its character references decoded, plus the raw offset each decoded character
    /// starts at. Required to inject into `Spice & Wolf`
    /// </summary>
    private static (string Text, int[] RawStarts) DecodeWithMap(string raw)
    {
        var builder = new StringBuilder(raw.Length);
        var rawStarts = new List<int>(raw.Length + 1);
        var i = 0;

        while (i < raw.Length)
        {
            var length = 1;
            var decoded = raw[i].ToString();

            if (raw[i] == '&')
            {
                var semicolon = raw.IndexOf(';', i + 1);
                if (semicolon > i && semicolon - i <= MaxCharacterReferenceLength)
                {
                    var candidate = raw[i..(semicolon + 1)];
                    var entity = HtmlEntity.DeEntitize(candidate);
                    // DeEntitize hands back anything it doesn't recognise unchanged, so a difference means a reference
                    if (entity.Length == 1 && !string.Equals(entity, candidate, StringComparison.Ordinal))
                    {
                        decoded = entity;
                        length = candidate.Length;
                    }
                }
            }

            rawStarts.Add(i);
            builder.Append(decoded);
            i += length;
        }

        rawStarts.Add(i); // The boundary at the end of the text

        return (builder.ToString(), [.. rawStarts]);
    }

    /// <summary>
    /// Start offsets and lengths of each element's text within the joined text of the range.
    /// </summary>
    private static List<(int StartPos, int TextLength)> BuildElementTextMappings(List<HtmlNode> elements)
    {
        var mappings = new List<(int, int)>();
        var currentPos = 0;

        foreach (var element in elements)
        {
            var textLength = GetTextForHighlighting(element).Length;

            mappings.Add((currentPos, textLength));
            currentPos += textLength + ElementTextSeparator.Length;
        }

        return mappings;
    }

    private static string NormalizeWhitespace(string text)
    {
        return WhitespaceRegex().Replace(text.Trim(), " ");
    }

    private static HtmlNode? NormalizeToBlockElement(HtmlNode? node)
    {
        while (node != null && InlineTags.Contains(node.Name))
        {
            node = node.ParentNode;
        }

        return node;
    }

    private static int MapNormalizedPositionToText(string text, int normalizedPosition)
    {
        var normalizedText = NormalizeWhitespace(text);

        if (normalizedPosition >= normalizedText.Length) return text.Length;

        // Walk through both strings character by character to find the mapping
        var pos = 0;
        var normalizedPos = 0;

        while (pos < text.Length && char.IsWhiteSpace(text[pos]))
        {
            pos++;
        }

        while (pos < text.Length && normalizedPos < normalizedPosition)
        {
            if (char.IsWhiteSpace(text[pos]))
            {
                // Skip consecutive whitespace in text
                while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                {
                    pos++;
                }
            }
            else
            {
                pos++;
            }

            // This corresponds to one space in normalized text
            normalizedPos++;
        }

        return pos;
    }

    private static HtmlNode? FindElementByXPath(HtmlDocument doc, string xpath)
    {
        var idMatch = IdXPathRegex().Match(xpath);
        if (!idMatch.Success) return doc.DocumentNode.SelectSingleNode(xpath.ToLowerInvariant());

        var id = idMatch.Groups[1].Value;
        return string.IsNullOrWhiteSpace(id) ? null : doc.GetElementbyId(id);
    }

    private static List<HtmlNode> GetElementsInRange(HtmlNode startElement, HtmlNode endElement)
    {
        var elements = new List<HtmlNode>();
        var current = startElement;

        // If start and end are the same, return just that element
        if (startElement == endElement)
        {
            elements.Add(current);
            return elements;
        }

        // Traverse siblings until we reach the end element
        while (current != null && current != endElement)
        {
            if (current.NodeType == HtmlNodeType.Element)
                elements.Add(current);

            current = current.NextSibling;
        }

        if (current == endElement)
        {
            elements.Add(endElement);
        }


        return elements;
    }
}
