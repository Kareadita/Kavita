﻿using System.Collections.Immutable;

namespace API.Constants;

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
    /// <summary>
    /// Used to give a user ability to change their own password
    /// </summary>
    public const string ChangePasswordRole = "Change Password";
    /// <summary>
    /// Used to give a user ability to bookmark files on the server
    /// </summary>
    public const string BookmarkRole = "Bookmark";
    /// <summary>
    /// Used to give a user ability to Change Restrictions on their account
    /// </summary>
    public const string ChangeRestrictionRole = "Change Restriction";
    /// <summary>
    /// Used to give a user ability to Login to their account
    /// </summary>
    public const string LoginRole = "Login";

    public static readonly ImmutableArray<string> ValidRoles =
        ImmutableArray.Create(AdminRole, PlebRole, DownloadRole, ChangePasswordRole, BookmarkRole, ChangeRestrictionRole, LoginRole);
}
