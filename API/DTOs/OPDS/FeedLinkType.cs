namespace API.DTOs.OPDS;

public static class FeedLinkType
{
    public const string Atom = "application/atom+xml";
    public const string AtomSearch = "application/opensearchdescription+xml";
    public const string AtomNavigation = "application/atom+xml;profile=opds-catalog;kind=navigation";
    public const string AtomAcquisition = "application/atom+xml;profile=opds-catalog;kind=acquisition";
    public const string Image = "image/jpeg";
}
