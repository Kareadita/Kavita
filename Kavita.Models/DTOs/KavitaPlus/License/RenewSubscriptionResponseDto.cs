namespace Kavita.Models.DTOs.KavitaPlus.License;
#nullable enable

public sealed record RenewSubscriptionResponseDto
{
    /// <summary>
    /// The Stripe Checkout (Pay Now) URL the customer visits to pay and reactivate their subscription.
    /// </summary>
    public string? CheckoutUrl { get; set; }
}
