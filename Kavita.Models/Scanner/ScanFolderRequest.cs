namespace Kavita.Models.Scanner;

/// <param name="LibraryId">The library whose root holds <paramref name="Folder"/></param>
/// <param name="Folder">The top folder under the library root, normalized. The root itself when the root was asked for</param>
/// <param name="ChangedPath">The file or folder that changed or was asked for, normalized</param>
/// <param name="FromFolderWatcher">False when the scan-folder API asked for it</param>
/// <param name="AbortOnNoSeriesMatch">Do not fall back to a library scan when no single series owns the path</param>
public sealed record ScanFolderRequest(int LibraryId, string Folder, string ChangedPath, bool FromFolderWatcher, bool AbortOnNoSeriesMatch);
