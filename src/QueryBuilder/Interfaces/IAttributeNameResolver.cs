using System.Linq.Expressions;
using Microsoft.Xrm.Sdk;

namespace DataverseQuery.QueryBuilder.Interfaces
{
    /// <summary>
    /// Resolves attribute names from lambda expressions.
    /// </summary>
    public interface IAttributeNameResolver
    {
        /// <summary>
        /// Gets the attribute name from a property selector expression.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <typeparam name="TValue">The value type of the property.</typeparam>
        /// <param name="fieldSelector">The lambda expression selecting the property.</param>
        /// <returns>The attribute name, or null if it cannot be resolved.</returns>
        string? GetAttributeName<TEntity, TValue>(Expression<Func<TEntity, TValue>> fieldSelector)
            where TEntity : Entity;
    }
}
