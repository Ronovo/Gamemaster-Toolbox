using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace GM_Toolbox.Services
{
    public static class QueryFilters
    {
        /// <summary>The rows whose text contains the term, ignoring case; every row when the term is blank.</summary>
        public static IQueryable<T> Containing<T>(this IQueryable<T> query, Expression<Func<T, string>> text, string? term)
        {
            var needle = term?.Trim().ToLower();
            if (string.IsNullOrEmpty(needle))
            {
                return query;
            }

            Expression<Func<string, bool>> contains = s => s.ToLower().Contains(needle);
            var body = ReplacingExpressionVisitor.Replace(contains.Parameters[0], text.Body, contains.Body);
            return query.Where(Expression.Lambda<Func<T, bool>>(body, text.Parameters));
        }
    }
}
