namespace Kavita.Models.Images;

/// <summary>
/// One entity whose cover is <see cref="ImageFileId"/>, named by its type (<c>Chapter</c>, <c>Series</c>, ...) and id
/// </summary>
public sealed record ImageFileHolder(int ImageFileId, string Holder, int HolderId);
