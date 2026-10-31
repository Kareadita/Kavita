namespace Kavita.Models.Entities.Enums;

/// <summary>
/// What the debug export puts in place of a value. Null and empty values are always left as they are
/// </summary>
public enum ExportScrubKind
{
    /// <summary>
    /// Random characters, the same length as the original
    /// </summary>
    Random = 0,
    /// <summary>
    /// <c>user-1@scrubbed.invalid</c>
    /// </summary>
    Email = 1,
    /// <summary>
    /// An address from the RFC 5737 documentation range. One original address maps to one replacement everywhere
    /// </summary>
    IpAddress = 2,
    /// <summary>
    /// A URL or host name with the host replaced, keeping the scheme, port and path
    /// </summary>
    Host = 3,
    Guid = 4,
    Phone = 5,
    /// <summary>
    /// <c>user-&lt;Id&gt;</c>, the same stand-in the logs get
    /// </summary>
    Username = 6,
    /// <summary>
    /// Text a user wrote. Words and HTML attribute values become junk of the same length, tag and attribute names are kept
    /// </summary>
    FreeText = 7,
    /// <summary>
    /// Known secrets, usernames, emails and IP addresses are replaced inside otherwise kept text, as in the logs
    /// </summary>
    TextScrub = 8,
    /// <summary>
    /// A hash, hashed again with a salt made for each export, so equal values stay equal
    /// </summary>
    Fingerprint = 9,
    /// <summary>
    /// A JSON value whose fields are classified on the type it holds
    /// </summary>
    Json = 10,
    DropTable = 11,
}
