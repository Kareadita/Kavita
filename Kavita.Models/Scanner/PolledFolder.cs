namespace Kavita.Models.Scanner;

/// <param name="Path">A library folder</param>
/// <param name="Mount">Null when the mount could not be read</param>
public sealed record PolledFolder(string Path, MountInfo Mount);
