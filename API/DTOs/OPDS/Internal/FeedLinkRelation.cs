namespace API.DTOs.OPDS;

public static class FeedLinkRelation
{
    public const string Debug = "debug";
    public const string Search = "search";
    public const string Self = "self";
    public const string Start = "start";
    public const string Next = "next";
    public const string Prev = "prev";
    public const string Alternate = "alternate";
    public const string SubSection = "subsection";
    public const string Related = "related";
    public const string Image = "http://opds-spec.org/image";
    public const string Thumbnail = "http://opds-spec.org/image/thumbnail";
    /// <summary>
    /// This will allow for a download to occur
    /// </summary>
    public const string Acquisition = "http://opds-spec.org/acquisition/open-access";
#pragma warning disable S1075
    public const string Stream = "http://vaemendis.net/opds-pse/stream";
#pragma warning restore S1075
}
