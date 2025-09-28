namespace API.DTOs.OPDS.Requests;


public sealed record OpdsCatalogueRequest : IOpdsRequest
{
    public string ApiKey { get; init; }
    public string Prefix { get; init; }
    public string BaseUrl { get; init; }
    public int UserId { get; init; }
}
