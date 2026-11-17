namespace Kavita.Models.DTOs.SignalR.Bodies;
#nullable enable

/// <param name="Corrected">Images resized, re-encoded or renamed</param>
/// <param name="Deleted">Files removed, unused rows' files and files no row names</param>
/// <param name="BytesSaved">Size on disk before minus after, deleted files included</param>
/// <param name="Missing">Rows whose file is not on disk, left as they are</param>
/// <param name="Unreadable">Files that could not be decoded, left as they are</param>
public sealed record ImageCleanupSummaryEventBodyDto(string Name, string Title, string SubTitle, int Corrected, int Deleted,
    long BytesSaved, int Missing, int Unreadable);
