using System;

namespace API.DTOs.License;

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
}
