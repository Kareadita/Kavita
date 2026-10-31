namespace Kavita.Models.DTOs.Kobo;

public sealed record KoboBookPriceInformationDto
{
    public string Id { get; set; }
    public string CrossRevisionId { get; set; }
    public KoboPriceDto Price { get; set; } = new();
    public bool EligibleForKoboLoveDiscount { get; set; } = false;
    public bool IsPreOrder { get; set; } = false;
}
