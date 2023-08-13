using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Query;
using MusicDataService.Controllers;

namespace MusicDataService.Extensions;

public enum SortOrder
{
    Ascending,
    Descending,
}

public static class IQueryableExtensions
{
    public static IOrderedQueryable<TSource> OrderByEx<TSource, TKey>(this IQueryable<TSource> queryable, Expression<Func<TSource, TKey>> selector, SortOrder sortOrder)
    {
        return sortOrder == SortOrder.Ascending ? queryable.OrderBy(selector) : queryable.OrderByDescending(selector);
    }

    //public static IIncludableQueryable<TEntity, TProperty> OrderByEx<TEntity, TProperty, TKey>(
    //    this IIncludableQueryable<TEntity, TProperty> queryable, Expression<Func<TEntity, TKey>> selector,
    //    SortOrder sortOrder)
    //{
    //    return sortOrder == SortOrder.Ascending ? queryable.OrderBy(selector) : queryable.OrderByDescending(selector);
    //}
}