using System.Collections.Generic;
using Kavita.Models.Entities.Progress;

namespace Kavita.API.Store;

public interface IUserContext
{
    /// <summary>
    /// Gets the current authenticated user's ID.
    /// Returns null if user is not authenticated or on [AllowAnonymous] endpoint.
    /// </summary>
    int? GetUserId();

    /// <summary>
    /// Gets the current authenticated user's ID.
    /// Throws KavitaException if user is not authenticated.
    /// </summary>
    int GetUserIdOrThrow();

    /// <summary>
    /// Gets the current authenticated user's username.
    /// Returns null if user is not authenticated.
    /// </summary>
    /// <remarks>Warning! Username's can contain .. and /, do not use folders or filenames explicitly with the Username</remarks>
    string? GetUsername();
    /// <summary>
    /// The Roles associated with the Authenticated user
    /// </summary>
    IReadOnlyList<string> Roles { get; }
    /// <summary>
    /// Returns true if the current user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
    /// <summary>
    /// Gets the authentication method used (JWT, Auth Key, OIDC).
    /// </summary>
    AuthenticationType GetAuthenticationType();


    bool HasRole(string role);
    bool HasAnyRole(params string[] roles);
    bool HasAllRoles(params string[] roles);
}
