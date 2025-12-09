using System;
using System.Linq;
using API.Entities.User;

namespace API.Extensions.QueryExtensions;

public static class AuthKeyQueryExtensions
{
    public static IQueryable<AppUserAuthKey> HasNotExpired(this IQueryable<AppUserAuthKey> queryable)
    {
        return queryable.Where(k => k.ExpiresAtUtc == null || k.ExpiresAtUtc > DateTime.UtcNow);
    }
}
