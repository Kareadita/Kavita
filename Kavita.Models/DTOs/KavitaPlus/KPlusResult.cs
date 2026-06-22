using System.Diagnostics.CodeAnalysis;

namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

public sealed record KPlusResult<T>
{
    [MemberNotNullWhen(true, nameof(Data))]
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }

    public static KPlusResult<T> Success(T data) => new() { IsSuccess = true, Data = data };
    public static KPlusResult<T> Failure(string errorMessage) => new() { IsSuccess = false, ErrorMessage = errorMessage };
}
