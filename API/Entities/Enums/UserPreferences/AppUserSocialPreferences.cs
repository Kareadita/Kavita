using System.Collections.Generic;

namespace API.Entities.Enums.UserPreferences;

public class AppUserSocialPreferences
{
    /// <summary>
    /// UI Site Global Setting: Should series reviews be shared with all users in the server
    /// </summary>
    public bool ShareReviews { get; set; } = false;

    /// <summary>
    /// UI Site Global Setting: Share your annotations with other users
    /// </summary>
    public bool ShareAnnotations { get; set; } = false;

    /// <summary>
    /// UI Site Global Setting: See other users' annotations while reading
    /// </summary>
    public bool ViewOtherAnnotations { get; set; } = false;

    /// <summary>
    /// UI Site Global Setting: For which libraries should social features be enabled
    /// </summary>
    /// <remarks>Empty array means all, disable specific social features to opt out everywhere</remarks>
    public IList<int> SocialLibraries { get; set; } = [];

    /// <summary>
    /// UI Site Global Setting: Highest age rating for which social features are enabled
    /// </summary>
    public AgeRating SocialMaxAgeRating { get; set; } = AgeRating.NotApplicable;

    /// <summary>
    /// UI Site Global Setting: Enable social features for unknown age ratings
    /// </summary>
    public bool SocialIncludeUnknowns { get; set; } = true;
}
