namespace Kavita.Models.DTOs.Email;

public sealed record ConfirmationEmailDto
{
    public string InvitingUser { get; init; } = default!;
    /// <summary>
    /// Who is receiving the email
    /// </summary>
    public required int EmailUserId { get; init; }
    public string EmailAddress { get; init; } = default!;
    public string ServerConfirmationLink { get; init; } = default!;
    /// <summary>
    /// InstallId of this Kavita Instance
    /// </summary>
    public string InstallId { get; init; } = default!;
}
