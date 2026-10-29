using Kavita.Models.Attributes;
using Kavita.Models.Entities.Enums;

namespace Kavita.Models.DTOs.KavitaPlus;

public sealed record KavitaPlusUserInfo
{
    [ExportScrub(ExportScrubKind.Username)]
    public required string Username { get; set; }
}
