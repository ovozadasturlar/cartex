namespace Cartex.Application.Common.Extensions;

using System.Linq.Expressions;

public static class DynamicOrderingExtensions
{
    public static IQueryable<T> OrderByDynamic<T>(this IQueryable<T> source, string propertyName)
        => ApplyOrder(source, propertyName, "OrderBy");

    public static IQueryable<T> OrderByDescendingDynamic<T>(this IQueryable<T> source, string propertyName)
        => ApplyOrder(source, propertyName, "OrderByDescending");

    public static IQueryable<T> ThenByDescendingDynamic<T>(this IQueryable<T> source, string propertyName)
        => ApplyOrder(source, propertyName, "ThenByDescending");

    private static IQueryable<T> ApplyOrder<T>(IQueryable<T> source, string propertyName, string methodName)
    {
        var param = Expression.Parameter(typeof(T), "x");
        var prop = typeof(T).GetProperties()
            .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Property '{propertyName}' not found on {typeof(T).Name}", nameof(propertyName));

        var property = Expression.Property(param, prop.Name);
        var lambda = Expression.Lambda(property, param);
        var method = typeof(Queryable).GetMethods()
            .First(m => m.Name == methodName && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), prop.PropertyType);

        return (IQueryable<T>)method.Invoke(null, [source, lambda])!;
    }
}
