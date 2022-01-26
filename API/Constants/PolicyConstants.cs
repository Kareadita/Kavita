using System.Collections.Generic;

namespace API.Constants
{
    /// <summary>
    /// Role-based Security
    /// </summary>
    public static class PolicyConstants
    {
        /// <summary>
        /// Admin User. Has all privileges
        /// </summary>
        public const string AdminRole = "Admin";
        /// <summary>
        /// Non-Admin User. Must be granted privileges by an Admin.
        /// </summary>
        public const string PlebRole = "Pleb";
        /// <summary>
        /// Used to give a user ability to download files from the server
        /// </summary>
        public const string DownloadRole = "Download";

        public static readonly IList<string> ValidRoles = new List<string>()
        {
            AdminRole, PlebRole, DownloadRole
        };
    }
}
