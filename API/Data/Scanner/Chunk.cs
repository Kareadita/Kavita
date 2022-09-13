namespace API.Data.Scanner;

/// <summary>
/// Represents a set of Entities which is broken up and iterated on
/// </summary>
public class Chunk
{
    /// <summary>
    /// Total number of entities
    /// </summary>
    public int TotalSize { get; set; }
    /// <summary>
    /// Size of each chunk to iterate over
    /// </summary>
    public int ChunkSize { get; set; }
    /// <summary>
    /// Total chunks to iterate over
    /// </summary>
    public int TotalChunks { get; set; }
}
