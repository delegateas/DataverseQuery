using System.Linq.Expressions;
using DataverseQuery.QueryBuilder.Interfaces;
using Microsoft.Xrm.Sdk;

namespace DataverseQuery.QueryBuilder.Services
{
    /// <summary>
    /// Default implementation of attribute name resolution.
    /// </summary>
    public sealed class AttributeNameResolver : IAttributeNameResolver
    {
        /// <summary>
        /// Gets the attribute name from a property selector expression.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <typeparam name="TValue">The value type of the property.</typeparam>
        /// <param name="fieldSelector">The lambda expression selecting the property.</param>
        /// <returns>The attribute name in lowercase, or null if it cannot be resolved.</returns>
        public string? GetAttributeName<TEntity, TValue>(Expression<Func<TEntity, TValue>> fieldSelector)
            where TEntity : Entity
        {
            ArgumentNullException.ThrowIfNull(fieldSelector);

            return fieldSelector.Body switch
            {
                MemberExpression member => member.Member.Name.ToLowerInvariant(),
                UnaryExpression { Operand: MemberExpression unaryMember } => unaryMember.Member.Name.ToLowerInvariant(),
                _ => null,
            };
        }
    }
}
