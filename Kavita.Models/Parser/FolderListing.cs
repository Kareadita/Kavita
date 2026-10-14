using System.Collections.Generic;

namespace Kavita.Models.Parser;
#nullable enable

/// <summary>
/// One folder's listing: its subfolders (not normalized) and the library files directly in it
/// </summary>
public sealed record FolderListing(List<string> Subfolders, List<FileStamp> Files);
