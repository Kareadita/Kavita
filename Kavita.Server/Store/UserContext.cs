using System;
using System.Collections.Generic;
using System.Linq;
using Kavita.API.Store;
using Kavita.Common;
using Kavita.Models.Entities.Progress;

namespace Kavita.Server.Store;

public class UserContext : IUserContext
{
    private int? _userId;
    private string? _username;
    private AuthenticationType _authType;
    private List<string> _roles = new();

    public int? GetUserId() => _userId;

    public int GetUserIdOrThrow()
    {
        return _userId ?? throw new UnauthorizedAccessException();
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
