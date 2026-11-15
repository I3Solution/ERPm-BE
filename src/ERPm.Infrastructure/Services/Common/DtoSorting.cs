using System.Linq.Expressions;
using System.Reflection;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;

namespace ERPm.Infrastructure.Services.Common;

/// <summary>Sorts a projected list by the DTO property the client asked for.</summary>
public static class DtoSorting
{
    /// <summary>True when a sort column was requested.</summary>
    public static bool IsRequested(ListSort? sort) => !string.IsNullOrWhiteSpace(sort?.SortBy);

    /// <summary>
    /// Orders by the requested scalar DTO property (JSON or property casing), then by the row key so paging is stable.
    /// An unknown or non-scalar name is a 400 error.validation.request.
    /// </summary>
    public static IQueryable<T> OrderByRequested<T>(this IQueryable<T> query, ListSort sort)
    {
        var property = typeof(T).GetProperty(sort.SortBy!.Trim(), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property == null || !(property.PropertyType.IsValueType || property.PropertyType == typeof(string)))
            throw new BusinessException("error.validation.request");
        var ordered = Order(query, property, sort.IsAscending ? "OrderBy" : "OrderByDescending");
        // Ties break on the row key (Id, or ItemId/WarehouseId for stock balances) in the same direction, so paging is stable.
        foreach (var key in new[] { "Id", "ItemId", "WarehouseId" })
        {
            var tie = typeof(T).GetProperty(key);
            if (tie != null && tie != property) ordered = Order(ordered, tie, sort.IsAscending ? "ThenBy" : "ThenByDescending");
        }
        return ordered;
    }

    private static IQueryable<T> Order<T>(IQueryable<T> query, PropertyInfo property, string method)
    {
        var argument = Expression.Parameter(typeof(T), "e");
        var selector = Expression.Lambda(Expression.Property(argument, property), argument);
        var call = Expression.Call(typeof(Queryable), method, [typeof(T), property.PropertyType], query.Expression, Expression.Quote(selector));
        return query.Provider.CreateQuery<T>(call);
    }
}
