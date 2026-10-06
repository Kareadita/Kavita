using System.ComponentModel;

namespace Kavita.Models.Entities.Enums;

public enum MediaErrorReason
{
    [Description("Unknown")]
    Unknown = 0,
    [Description("Parse Failed")]
    ParseFailed = 1,
    [Description("No Series Name")]
    NoSeriesName = 2,
    [Description("Unreadable Archive")]
    UnreadableArchive = 3,
    [Description("Unsupported Format")]
    UnsupportedFormat = 4,
    [Description("Corrupt Epub")]
    CorruptEpub = 5,
    [Description("No Paged")]
    NoPages = 6,
    [Description("Cover Failed")]
    CoverFailed = 7,
    [Description("Word Count Failed")]
    WordCountFailed = 8,
    [Description("I/O Error")]
    IoError = 9,
    [Description("Metadata Unreadable")]
    MetadataUnreadable = 10,
    [Description("Epub Not Strict")]
    EpubNotStrict = 11,
}

public static class MediaErrorReasons
{
    /// <summary>
    /// The scanner still imported a file with one of these, so it is not a file that "could not be read"
    /// </summary>
    public static readonly MediaErrorReason[] Imported = [MediaErrorReason.MetadataUnreadable, MediaErrorReason.EpubNotStrict];
}
