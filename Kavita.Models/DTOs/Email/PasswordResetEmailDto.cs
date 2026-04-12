namespace Kavita.Models.DTOs.Email;

public sealed record PasswordResetEmailDto
{
    public required int EmailUserId { get; init; }
    public string EmailAddress { get; init; } = default!;
    public string ServerConfirmationLink { get; init; } = default!;
}
