using AutoMapper;

namespace TlmcPlayerBackend.Utils.Extensions;

public static class AutoMapperExtensions
{
    public static IMappingExpression<TSource, TDest> IgnoreAllUnmapped<TSource, TDest>(this IMappingExpression<TSource, TDest> expression) { 
        expression.ForAllMembers(opt => opt.Ignore()); 
        return expression;
    }
}