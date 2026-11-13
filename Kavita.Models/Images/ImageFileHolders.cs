using System.Collections.Generic;
using Kavita.Models.Entities;

namespace Kavita.Models.Images;

#nullable enable

/// <summary>
/// The entities pointing at one <see cref="ImageFile"/>. Only chapters, volumes and series share a row
/// </summary>
public sealed record ImageFileHolders(IList<Chapter> Chapters, IList<Volume> Volumes, IList<Series> Series);
