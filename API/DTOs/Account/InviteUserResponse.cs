﻿namespace API.DTOs.Account;

public class InviteUserResponse
{
    /// <summary>
    /// Email link used to setup the user account
    /// </summary>
    public string EmailLink { get; set; }
    /// <summary>
    /// Was an email sent (ie is this server accessible)
    /// </summary>
    public bool EmailSent { get; set; }
}
