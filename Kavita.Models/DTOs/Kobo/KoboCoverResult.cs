namespace Kavita.Models.DTOs.Kobo;

/// <summary>
/// Cover image path for a Kobo entitlement (chapter → volume → series fallback).
/// </summary>
public class KoboCoverResult
{
    public required string FilePath { get; init; }
    public required string ContentType { get; init; }
}