using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace Kavita.Database.Extensions;

public static class ExpressionExtensions
{
    public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var body = ReplacingExpressionVisitor.Replace(
            right.Parameters[0], left.Parameters[0], right.Body);

        return Expression.Lambda<Func<T, bool>>(
            Expression.OrElse(left.Body, body), left.Parameters);
    }
}
