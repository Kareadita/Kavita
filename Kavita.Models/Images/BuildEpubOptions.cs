using System.Collections.Generic;
using Kavita.Models.DTOs;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.Images;

public sealed record BuildEpubOptions(string BookTitle, List<string> Pages, ImageRender Render,
    ReadingDirection ReadingDirection, ChapterDto Chapter, string OutputPath); // I probably need Metadata and explicit cover
