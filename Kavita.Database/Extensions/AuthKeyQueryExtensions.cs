using System;
using System.Linq;
using Kavita.Models.Entities.User;

namespace Kavita.Database.Extensions;

public static class AuthKeyQueryExtensions
{
    public static IQueryable<AppUserAuthKey> HasNotExpired(this IQueryable<AppUserAuthKey> queryable)
    {
        return queryable.Where(k => k.ExpiresAtUtc == null || k.ExpiresAtUtc > DateTime.UtcNow);
    }
}

