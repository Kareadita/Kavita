namespace Kavita.Models.Scanner;

/// <param name="Folder">The top folder under the library root, normalized</param>
/// <param name="ChangedPath">The file or folder that changed, normalized. Empty when the whole folder is asked for</param>
/// <param name="AbortOnNoSeriesMatch">Do not fall back to a library scan when no single series owns the path</param>
public sealed record ScanFolderRequest(string Folder, string ChangedPath, bool AbortOnNoSeriesMatch);
