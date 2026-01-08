using System;
using System.Collections.Generic;
using System.Linq;
using API.Entities.Progress;
using Kavita.Common;

namespace API.Services.Store;
#nullable enable

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

public class UserContext : IUserContext
{
    private int? _userId;
    private string? _username;
    private AuthenticationType _authType;
    private List<string> _roles = new();

    public int? GetUserId() => _userId;

    public int GetUserIdOrThrow()
    {
        // TODO: Refactor this to use ProblemDetails and handle appropriately
        return _userId ?? throw new KavitaException("User is not authenticated");
    }

    public string? GetUsername() => _username;

    public AuthenticationType GetAuthenticationType() => _authType;

    public bool IsAuthenticated { get; private set; }
    public IReadOnlyList<string> Roles => _roles.AsReadOnly();

    // Internal method used by middleware to set context
    internal void SetUserContext(int userId, string username, AuthenticationType authType, IEnumerable<string> roles)
    {
        _userId = userId;
        _username = username;
        _authType = authType;
        IsAuthenticated = true;
        _roles = roles?.ToList() ?? [];
    }

    internal void Clear()
    {
        _userId = null;
        _username = null;
        _authType = AuthenticationType.Unknown;
        IsAuthenticated = false;
        _roles.Clear();
    }

    public bool HasRole(string role)
    {
        return _roles.Any(r => r.Equals(role, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasAnyRole(params string[] roles)
    {
        return roles.Any(HasRole);
    }

    public bool HasAllRoles(params string[] roles)
    {
        return roles.All(HasRole);
    }
}
