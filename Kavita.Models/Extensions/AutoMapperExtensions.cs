using System;
using System.Linq.Expressions;
using AutoMapper;

namespace Kavita.Models.Extensions;

public static class AutoMapperExtensions
{
    public static IMappingExpression<TSource, TDestination> ForMemberSimple<TSource, TDestination, TMember>(
        this IMappingExpression<TSource, TDestination> expression,
        Expression<Func<TDestination,TMember>> destinationMember,
        Expression<Func<TSource, TMember>> mapExpression
        )
    {
        return expression.ForMember(destinationMember, opt => opt.MapFrom(mapExpression));
    }
}
