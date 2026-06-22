namespace Kavita.Models.DTOs.KavitaPlus.License;

public sealed record LinkDiscordRequestDto
{
    public required string DiscordId { get; set; }
    public required string DiscordUserName { get; set; }
}
