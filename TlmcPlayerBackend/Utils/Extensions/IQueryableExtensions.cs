using System.Linq.Expressions;

namespace TlmcPlayerBackend.Utils.Extensions;

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

    public static IOrderedQueryable<TSource> ThenByEx<TSource, TKey>(this IOrderedQueryable<TSource> orderedQueryable,
        Expression<Func<TSource, TKey>> selector, SortOrder sortOrder)
    {
        return sortOrder == SortOrder.Ascending ? orderedQueryable.ThenBy(selector) : orderedQueryable.ThenByDescending(selector);
    }
}