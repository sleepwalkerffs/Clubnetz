using System.Linq.Expressions;
using Bookennis.Shared.Utils.Sorting;

namespace Bookennis.Api.Infrastructure.Utils.Sorting;

public static class SortingExtensions
{
    public static IQueryable<T> Sort<T>(this IQueryable<T> query, ISortingQuery sortingQuery)
        => query.Sort(sortingQuery.Sort);

    public static IQueryable<T> Sort<T>(this IQueryable<T> query, SortParameters? arguments)
    {
        if (arguments == null || arguments.Count == 0)
            return query;

        var isFirstOrderByStatement = true;
        foreach (var (column, direction) in arguments)
        {
            // Creates a `query.OrderBy(p => p.<arg0>)` or a `query.ThenBy(p => p.<argN>)` expression
            var param = Expression.Parameter(typeof(T), "p");
            var property = CreateMemberExpression(param, column);
            if (property != null)
            {
                var sort = Expression.Lambda(property, param);
                var call = Expression.Call(
                    typeof(Queryable),
                    (isFirstOrderByStatement ? "OrderBy" : "ThenBy") + (direction == SortDirection.Descending ? "Descending" : string.Empty),
                    [typeof(T), property.Type],
                    query.Expression,
                    Expression.Quote(sort));

                query = query.Provider.CreateQuery<T>(call);

                isFirstOrderByStatement = false;
            }
        }

        return query;
    }

    private static MemberExpression? CreateMemberExpression(ParameterExpression parameterExpression, string propertyName)
    {
        try
        {
            Expression body = parameterExpression;
            foreach (var member in propertyName.Split('.'))
            {
                body = Expression.PropertyOrField(body, member);
            }

            return (MemberExpression)body;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}