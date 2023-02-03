using System;
using API.Entities.Enums;

namespace API.DTOs;

public class MangaFileDto
{
    public int Id { get; init; }
    public string FilePath { get; init; }
    public int Pages { get; init; }
    public long Bytes { get; init; }
    public MangaFormat Format { get; init; }
    public DateTime Created { get; init; }

}
