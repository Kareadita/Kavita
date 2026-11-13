using System;
using System.Collections.Generic;

namespace Kavita.Models.DTOs.Kobo;

public sealed record KoboNewTagDto
{
    public KoboWrappedTagDto NewTag { get; set; }
}

public sealed record KoboChangedTagDto
{
    public KoboWrappedTagDto ChangedTag { get; set; }
}

public sealed record KoboDeletedTagDto
{
    public KoboWrappedTagDto DeletedTag { get; set; }
}

public sealed record KoboWrappedTagDto
{
    public KoboTagDto Tag { get; set; }
}

public sealed record KoboTagDto
{
    public string Id { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    public string Name { get; set; }
    public KoboTagType Type { get; set; } = KoboTagType.UserTag;
    public List<KoboTagItemDto> Items { get; set; }

}

public sealed record KoboTagItemDto
{
    public string RevisionId { get; set; }
    public string Type { get; set; } = "ProductRevisionTagItem";
}

public enum KoboTagType
{
    SystemTag,
    UserTag,
}
