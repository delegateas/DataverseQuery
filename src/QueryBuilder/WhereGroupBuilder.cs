using System.Linq.Expressions;
using DataverseQuery.QueryBuilder.Interfaces;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseQuery.QueryBuilder
{
    /// <summary>
    /// Builder for creating grouped filter conditions with a specific logical operator.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    public sealed class WhereGroupBuilder<TEntity>
        where TEntity : Entity
    {
        private readonly List<ConditionExpression> conditions = new();
        private readonly IAttributeNameResolver attributeNameResolver;
        private readonly IValueConverter valueConverter;

        /// <summary>
        /// Initializes a new instance of the <see cref="WhereGroupBuilder{TEntity}"/> class.
        /// </summary>
        /// <param name="attributeNameResolver">The service for resolving attribute names from expressions.</param>
        /// <param name="valueConverter">The service for converting values for Dataverse queries.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        internal WhereGroupBuilder(
            IAttributeNameResolver attributeNameResolver,
            IValueConverter valueConverter)
        {
            this.attributeNameResolver = attributeNameResolver ?? throw new ArgumentNullException(nameof(attributeNameResolver));
            this.valueConverter = valueConverter ?? throw new ArgumentNullException(nameof(valueConverter));
        }

        /// <summary>
        /// Adds a condition to this filter group.
        /// </summary>
        /// <typeparam name="TValue">The type of the field value.</typeparam>
        /// <param name="fieldSelector">The lambda expression selecting the field.</param>
        /// <param name="op">The condition operator to apply.</param>
        /// <param name="values">The values to compare against.</param>
        /// <returns>This WhereGroupBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when fieldSelector or values is null.</exception>
        public WhereGroupBuilder<TEntity> Where<TValue>(
            Expression<Func<TEntity, TValue>> fieldSelector,
            ConditionOperator op,
            params TValue[] values)
        {
            ArgumentNullException.ThrowIfNull(fieldSelector);
            ArgumentNullException.ThrowIfNull(values);

            var attributeName = attributeNameResolver.GetAttributeName(fieldSelector);
            if (!string.IsNullOrEmpty(attributeName))
            {
                var convertedValues = valueConverter.ConvertValues(values);
                conditions.Add(new ConditionExpression(attributeName, op, convertedValues));
            }

            return this;
        }

        /// <summary>
        /// Gets all conditions in this group.
        /// </summary>
        /// <returns>A read-only list of conditions in this group.</returns>
        internal IReadOnlyList<ConditionExpression> GetConditions() => conditions.AsReadOnly();

        /// <summary>
        /// Gets whether this group has any conditions.
        /// </summary>
        /// <returns>True if this group has conditions, false otherwise.</returns>
        internal bool HasConditions => conditions.Count > 0;
    }
}
