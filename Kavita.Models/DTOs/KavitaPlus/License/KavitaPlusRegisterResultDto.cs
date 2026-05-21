namespace Kavita.Models.DTOs.KavitaPlus.License;

public sealed record KavitaPlusRegisterResultDto
{
    public bool Success { get; set; }
    public KavitaPlusRegistrationErrorCode ErrorCode { get; set; }
}
