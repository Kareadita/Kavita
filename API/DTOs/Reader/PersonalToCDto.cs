﻿namespace API.DTOs.Reader;

public class PersonalToCDto
{
    public required int ChapterId { get; set; }
    public required int PageNumber { get; set; }
    public required string Title { get; set; }
    public string? BookScrollId { get; set; }
}
