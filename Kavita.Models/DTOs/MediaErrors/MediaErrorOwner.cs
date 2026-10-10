namespace Kavita.Models.DTOs.MediaErrors;

/// <param name="SeriesId">Null when no series has the file yet</param>
public sealed record MediaErrorOwner(int? LibraryId, int? SeriesId);
