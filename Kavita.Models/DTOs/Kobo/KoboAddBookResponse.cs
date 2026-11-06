using System.Collections.Generic;

namespace Kavita.Models.DTOs.Kobo;

public record KoboResult<T>
{
    public T Result { get; init; }
}

public sealed record KoboDetailResults<T>
{
    public List<T> DetailedResults { get; init; }
    public string RequestResult { get; init; } = "Success";
}

public sealed record KoboAddBookResultResponse : KoboResult<KoboDetailResults<KoboAddBookResponse>>;

public sealed record KoboAddBookResponse
{
    public string RevisionId { get; init; }
    public string CrossRevisionId { get; init; }
    public string EntitlementId { get; init; }
    public string Result { get; init; } = "Success";
}
