namespace API.Constants;

/// <summary>
/// Constants for Higher level policy roles
/// </summary>
public static class PolicyGroups
{
    /// <summary>
    /// Requires admin to execute
    /// </summary>
    public const string AdminPolicy = "RequireAdminRole";
    /// <summary>
    /// Requires Admin or Download to execute
    /// </summary>
    public const string DownloadPolicy = "RequireDownloadRole";
    /// <summary>
    /// Requires Admin or Change Password to execute
    /// </summary>
    public const string ChangePasswordPolicy = "RequireChangePasswordRole";
}
