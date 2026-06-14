namespace Kavita.Models.DTOs.KavitaPlus.License;
#nullable enable

public sealed record KavitaPlusProductInfo
{
    /// <summary>
    /// Stripe product name (e.g. "Kavita+")
    /// </summary>
    public string? ProductName { get; set; }

    /// <summary>
    /// List price in cents (0 = free). No customer context, so coupons/discounts are not applied.
    /// </summary>
    public long? PriceAmount { get; set; }

    /// <summary>
    /// ISO currency code (e.g. "usd")
    /// </summary>
    public string? PriceCurrency { get; set; }

    /// <summary>
    /// Billing cycle interval the renew request should send to select this product
    /// </summary>
    public KavitaPlusBillingInterval? BillingInterval { get; set; }
}
