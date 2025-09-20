using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using API.DTOs.Reader;
using HtmlAgilityPack;

namespace API.Helpers;
#nullable enable

public static partial class AnnotationHelper
{
    private const string UiXPathScope = "//BODY/DIV[1]"; // Div[1] is the div we inject reader contents into

    [GeneratedRegex("""^id\("([^"]+)"\)$""")]
    private static partial Regex IdXPathRegex();


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
                var scopedXPath = DescopeXpath(xpath);
                var elem = FindElementByXPath(doc, xpath);
                if (elem == null) continue;

                var originalText = elem.InnerText;

                // Calculate positions and sort by start position
                var normalizedOriginalText = NormalizeWhitespace(originalText);

                var sortedAnnotations = elementAnnotations
                    .Select(a => new
                    {
                        Annotation = a,
                        StartPos = normalizedOriginalText.IndexOf(NormalizeWhitespace(a.SelectedText), StringComparison.Ordinal)
                    })
                    .Where(a => a.StartPos >= 0)
                    .OrderBy(a => a.StartPos)
                    .ToList();

                elem.RemoveAllChildren();
                var currentPos = 0;

                foreach (var item in sortedAnnotations)
                {
                    // Add text before highlight
                    if (item.StartPos > currentPos)
                    {
                        var beforeText = originalText.Substring(currentPos, item.StartPos - currentPos);
                        elem.AppendChild(HtmlNode.CreateNode(beforeText));
                    }

                    // Add highlight
                    var highlightNode = HtmlNode.CreateNode(
                        $"<app-epub-highlight id=\"epub-highlight-{item.Annotation.Id}\">{item.Annotation.SelectedText}</app-epub-highlight>");
                    elem.AppendChild(highlightNode);

                    currentPos = item.StartPos + item.Annotation.SelectedText.Length;
                }

                // Add remaining text
                if (currentPos < originalText.Length)
                {
                    elem.AppendChild(HtmlNode.CreateNode(originalText.Substring(currentPos)));
                }
            }
            catch (Exception ex)
            {
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

                var startElement = FindElementByXPath(doc, startXPath);
                var endElement = FindElementByXPath(doc, endXPath);

                if (startElement == null || endElement == null) continue;

                // Get all elements between start and end (including start and end)
                var elementsInRange = GetElementsInRange(startElement, endElement);
                if (elementsInRange.Count == 0) continue;

                // Build full text to find our selection
                var fullText = string.Join("\n\n", elementsInRange.Select(e => e.InnerText));

                // Normalize both texts for comparison
                var normalizedFullText = NormalizeWhitespace(fullText);
                var normalizedSelectedText = NormalizeWhitespace(annotation.SelectedText);

                var selectionStartPos = normalizedFullText.IndexOf(normalizedSelectedText, StringComparison.Ordinal);

                if (selectionStartPos == -1) continue;

                var selectionEndPos = selectionStartPos + normalizedSelectedText.Length;

                // Map positions back to elements using the original (non-normalized) text
                var elementTextMappings = BuildElementTextMappings(elementsInRange);

                // Convert normalized positions back to original text positions
                var originalSelectionStart = MapNormalizedPositionToOriginal(fullText, selectionStartPos);
                var originalSelectionEnd = MapNormalizedPositionToOriginal(fullText, selectionEndPos);

                // Process each element in the range
                for (var i = 0; i < elementsInRange.Count; i++)
                {
                    var element = elementsInRange[i];
                    var mapping = elementTextMappings[i];

                    var elementStart = mapping.StartPos;
                    var elementEnd = mapping.EndPos;

                    // Determine what part of this element should be highlighted
                    var highlightStart = Math.Max(originalSelectionStart - elementStart, 0);
                    var highlightEnd = Math.Min(originalSelectionEnd - elementStart, mapping.TextLength);

                    if (highlightEnd <= highlightStart) continue; // No highlight in this element

                    InjectHighlightInElement(element, highlightStart, highlightEnd, annotation.Id);
                }
            }
            catch (Exception)
            {
                /* Swallow */
            }
        }
    }

    private static string NormalizeWhitespace(string text)
    {
        return WhitespaceRegex().Replace(text.Trim(), " ");
    }

    private static int MapNormalizedPositionToOriginal(string originalText, int normalizedPosition)
    {
        var normalizedText = NormalizeWhitespace(originalText);

        if (normalizedPosition == 0) return 0;
        if (normalizedPosition >= normalizedText.Length) return originalText.Length;

        // Walk through both strings character by character to find the mapping
        var originalPos = 0;
        var normalizedPos = 0;

        while (originalPos < originalText.Length && normalizedPos < normalizedPosition)
        {
            if (char.IsWhiteSpace(originalText[originalPos]))
            {
                // Skip consecutive whitespace in original
                while (originalPos < originalText.Length && char.IsWhiteSpace(originalText[originalPos]))
                {
                    originalPos++;
                }
            }
            else
            {
                originalPos++;
            }

            // This corresponds to one space in normalized text
            normalizedPos++;
        }

        return originalPos;
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

        elements.Add(current);

        // If start and end are the same, return just that element
        if (startElement == endElement) return elements;

        // Traverse siblings until we reach the end element
        while (current != null && current != endElement)
        {
            current = current.NextSibling;
            if (current is {NodeType: HtmlNodeType.Element}) // Only include element nodes (skip text nodes, comments, etc.)
            {
                elements.Add(current);
            }
        }

        return elements;
    }

    private static List<(int StartPos, int EndPos, int TextLength)> BuildElementTextMappings(List<HtmlNode> elements)
    {
        var mappings = new List<(int StartPos, int EndPos, int TextLength)>();
        var currentPos = 0;

        foreach (var element in elements)
        {
            var textLength = element.InnerText.Length;
            mappings.Add((currentPos, currentPos + textLength, textLength));
            currentPos += textLength;
        }

        return mappings;
    }

    private static void InjectHighlightInElement(HtmlNode element, int startPos, int endPos, int annotationId)
    {
        var originalText = element.InnerText;
        element.RemoveAllChildren();

        // Add text before highlight
        if (startPos > 0)
        {
            element.AppendChild(HtmlNode.CreateNode(originalText.Substring(0, startPos)));
        }

        // Add highlight
        var highlightText = originalText.Substring(startPos, endPos - startPos);
        var highlightNode = HtmlNode.CreateNode(
            $"<app-epub-highlight id=\"epub-highlight-{annotationId}\">{highlightText}</app-epub-highlight>");
        element.AppendChild(highlightNode);

        // Add text after highlight
        if (endPos < originalText.Length)
        {
            element.AppendChild(HtmlNode.CreateNode(originalText.Substring(endPos)));
        }
    }

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
