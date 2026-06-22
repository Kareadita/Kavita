using System;

namespace Kavita.Models.DTOs.KavitaPlus.License;
#nullable enable

public enum KavitaPlusSubscriptionState
{
    Active = 0,
    /// <summary>Only present for Kavita v0.9.0.6.</summary>
    Cancelled = 1,
    Paused = 2,
    /// <summary>Still grants access but set to cancel at period end (or past_due within grace)</summary>
    Cancelling = 3,
    /// <summary>No access remaining (ended, fully canceled, or past_due grace elapsed)</summary>
    Expired = 4
}

public enum KavitaPlusBillingInterval
{
    Day = 0,
    Week = 1,
    Month = 2,
    Year = 3
}

public sealed record LicenseInfoDto
{
    /// <summary>
    /// Overall subscription state
    /// </summary>
    public KavitaPlusSubscriptionState State { get; set; }

    /// <summary>
    /// Backward-compat shim - true when State is Active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Backward-compat shim - true when State is Cancelled
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    /// If cancelled, represents the cancellation/expiry date; if active, the next renewal date
    /// </summary>
    public DateTime ExpirationDate { get; set; }

    /// <summary>
    /// When the subscription will next renew; null if Cancelled or Paused
    /// </summary>
    public DateTime? NextChargeDate { get; set; }

    /// <summary>
    /// The earliest date this user ever subscribed, across all Stripe customer records with the same email
    /// </summary>
    public DateTime? SubscribedSince { get; set; }

    /// <summary>
    /// Stripe product name (e.g. "Kavita+")
    /// </summary>
    public string? ProductName { get; set; }

    /// <summary>
    /// Effective price in cents after any active coupon (0 = free)
    /// </summary>
    public long? PriceAmount { get; set; }

    /// <summary>
    /// ISO currency code (e.g. "usd")
    /// </summary>
    public string? PriceCurrency { get; set; }

    /// <summary>
    /// Billing cycle interval
    /// </summary>
    public KavitaPlusBillingInterval? BillingInterval { get; set; }

    /// <summary>
    /// True if an active coupon or discount is applied to the subscription or customer
    /// </summary>
    public bool HasActiveDiscount { get; set; }

    /// <summary>
    /// Is the installed version valid for Kavita+ (aka within 3 releases)
    /// </summary>
    /// <remarks>This is used only on Kavita</remarks>
    public bool IsValidVersion { get; set; }

    /// <summary>
    /// The email on file
    /// </summary>
    public string RegisteredEmail { get; set; }

    /// <summary>
    /// Total months subscribed across all historical subscriptions
    /// </summary>
    public int TotalMonthsSubbed { get; set; }
    /// <summary>
    /// A license is stored within Kavita
    /// </summary>
    public bool HasLicense { get; set; }
    /// <summary>
    /// InstallId which can be given to support
    /// </summary>
    public string InstallId { get; set; }

    /// <summary>
    /// In a Past Due state which we treat as cancelling
    /// </summary>
    public bool PastDue { get; set; }

    /// <summary>
    /// Discord UserId if set
    /// </summary>
    public string? DiscordId { get; set; }

    /// <summary>
    /// Discord Username if set
    /// </summary>
    public string? DiscordUsername { get; set; }

    /// <summary>
    /// Has Discord Set
    /// </summary>
    public bool HasDiscordSet => DiscordId is not null;

}
