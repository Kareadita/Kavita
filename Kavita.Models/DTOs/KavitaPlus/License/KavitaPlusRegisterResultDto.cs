using System.ComponentModel.DataAnnotations;
namespace Kavita.Models.DTOs.KavitaPlus.License;

public sealed record KavitaPlusRegisterResultDto
{
    public bool Success { get; set; }
    public bool IsSubscriptionActive { get; set; }
    [EnumDataType(typeof(KavitaPlusRegistrationErrorCode))]
    public KavitaPlusRegistrationErrorCode ErrorCode { get; set; }
}