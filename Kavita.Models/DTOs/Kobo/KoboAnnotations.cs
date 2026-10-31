using System;
using System.Collections.Generic;

namespace Kavita.Models.DTOs.Kobo;

public sealed record KoboPatchAnnotationsDto
{
    public List<KoboAnnotationDto> UpdatedAnnotations { get; set; }
}

public sealed record KoboAnnotationDto
{
    public DateTime ClientLastModifiedUtc { get; set; }
    public string HighlightColor { get; set; }
    public string HighlightedText { get; set; }
    public string Id { get; set; }
    public KoboAnnotationLocationDto Location { get; set; }
    public string noteText { get; set; }
    public string Type { get; set; } = "note";
}

public sealed record KoboAnnotationLocationDto
{
    public KoboAnnotationLocationSpanDto Span { get; set; }
}

public sealed record KoboAnnotationLocationSpanDto
{
    public string ChapterFilename { get; set; }
    /// <summary>
    /// Seems to be equal to <see cref="ChapterFilename"/>
    /// </summary>
    public string ChapterTitle { get; set; }
    public float ChapterProgress { get; set; }
    public int EndChar { get; set; }
    public int StartChar { get; set; }
    public string EndPath { get; set; }
    public string StartPath { get; set; }
}
