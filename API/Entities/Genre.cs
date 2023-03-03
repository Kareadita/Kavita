﻿using System.Collections.Generic;
using API.Entities.Metadata;
using Microsoft.EntityFrameworkCore;

namespace API.Entities;

[Index(nameof(NormalizedTitle), IsUnique = true)]
public class Genre
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? NormalizedTitle { get; set; }

    public ICollection<SeriesMetadata> SeriesMetadatas { get; set; } = null!;
    public ICollection<Chapter> Chapters { get; set; } = null!;
}
