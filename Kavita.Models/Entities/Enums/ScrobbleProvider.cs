using System;

namespace Kavita.Models.Entities.Enums;

/// <summary>
/// Misleading name but is the source of data (like a review coming from AniList)
/// </summary>
public enum ScrobbleProvider
{
    /// <summary>
    /// For now, this means data comes from within this instance of Kavita
    /// </summary>
    Kavita = 0,
    AniList = 1,
    Mal = 2,
    Cbr = 4,
    Hardcover = 5,
}
