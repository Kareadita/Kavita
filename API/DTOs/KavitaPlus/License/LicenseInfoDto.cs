using System;

namespace API.DTOs.KavitaPlus.License;

public class LicenseInfoDto
{
    /// <summary>
    /// If cancelled, will represent cancellation date. If not, will represent repayment date
    /// </summary>
    public DateTime ExpirationDate { get; set; }
    /// <summary>
    /// If cancelled or not
    /// </summary>
    public bool IsActive { get; set; }
    /// <summary>
    /// If will be or is cancelled
    /// </summary>
    public bool IsCancelled { get; set; }
    /// <summary>
    /// Is the installed version valid for Kavita+ (aka within 3 releases)
    /// </summary>
    public bool IsValidVersion { get; set; }
    /// <summary>
    /// The email on file
    /// </summary>
    public string RegisteredEmail { get; set; }
    /// <summary>
    /// Number of months user has been subscribed
    /// </summary>
    public int TotalMonthsSubbed { get; set; }
    /// <summary>
    /// A license is stored within Kavita
    /// </summary>
    public bool HasLicense { get; set; }
}
