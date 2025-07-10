using System.Linq.Expressions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseQuery.QueryBuilder.Extensions
{
    /// <summary>
    /// Extension methods for WhereGroupBuilder to provide common filter patterns.
    /// </summary>
    public static class WhereGroupBuilderExtensions
    {
        /// <summary>
        /// Adds an equality condition.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <typeparam name="TValue">The value type of the property.</typeparam>
        /// <param name="builder">The WhereGroupBuilder instance.</param>
        /// <param name="fieldSelector">The lambda expression selecting the field.</param>
        /// <param name="value">The value to compare against.</param>
        /// <returns>This WhereGroupBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when builder or fieldSelector is null.</exception>
        public static WhereGroupBuilder<TEntity> WhereEqual<TEntity, TValue>(
            this WhereGroupBuilder<TEntity> builder,
            Expression<Func<TEntity, TValue>> fieldSelector,
            TValue value)
            where TEntity : Entity
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.Where(fieldSelector, ConditionOperator.Equal, value);
        }

        /// <summary>
        /// Adds a 'like' condition.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <param name="builder">The WhereGroupBuilder instance.</param>
        /// <param name="fieldSelector">The lambda expression selecting the field.</param>
        /// <param name="pattern">The pattern to match (supports % wildcards).</param>
        /// <returns>This WhereGroupBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when builder or fieldSelector is null.</exception>
        public static WhereGroupBuilder<TEntity> WhereLike<TEntity>(
            this WhereGroupBuilder<TEntity> builder,
            Expression<Func<TEntity, string>> fieldSelector,
            string pattern)
            where TEntity : Entity
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.Where(fieldSelector, ConditionOperator.Like, pattern);
        }

        /// <summary>
        /// Adds an 'in' condition.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <typeparam name="TValue">The value type of the property.</typeparam>
        /// <param name="builder">The WhereGroupBuilder instance.</param>
        /// <param name="fieldSelector">The lambda expression selecting the field.</param>
        /// <param name="values">The values to check against.</param>
        /// <returns>This WhereGroupBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when builder or fieldSelector is null.</exception>
        public static WhereGroupBuilder<TEntity> WhereIn<TEntity, TValue>(
            this WhereGroupBuilder<TEntity> builder,
            Expression<Func<TEntity, TValue>> fieldSelector,
            params TValue[] values)
            where TEntity : Entity
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.Where(fieldSelector, ConditionOperator.In, values);
        }

        /// <summary>
        /// Adds a 'not equal' condition.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <typeparam name="TValue">The value type of the property.</typeparam>
        /// <param name="builder">The WhereGroupBuilder instance.</param>
        /// <param name="fieldSelector">The lambda expression selecting the field.</param>
        /// <param name="value">The value to compare against.</param>
        /// <returns>This WhereGroupBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when builder or fieldSelector is null.</exception>
        public static WhereGroupBuilder<TEntity> WhereNotEqual<TEntity, TValue>(
            this WhereGroupBuilder<TEntity> builder,
            Expression<Func<TEntity, TValue>> fieldSelector,
            TValue value)
            where TEntity : Entity
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.Where(fieldSelector, ConditionOperator.NotEqual, value);
        }

        /// <summary>
        /// Adds a 'greater than' condition.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <typeparam name="TValue">The value type of the property.</typeparam>
        /// <param name="builder">The WhereGroupBuilder instance.</param>
        /// <param name="fieldSelector">The lambda expression selecting the field.</param>
        /// <param name="value">The value to compare against.</param>
        /// <returns>This WhereGroupBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when builder or fieldSelector is null.</exception>
        public static WhereGroupBuilder<TEntity> WhereGreaterThan<TEntity, TValue>(
            this WhereGroupBuilder<TEntity> builder,
            Expression<Func<TEntity, TValue>> fieldSelector,
            TValue value)
            where TEntity : Entity
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.Where(fieldSelector, ConditionOperator.GreaterThan, value);
        }

        /// <summary>
        /// Adds a 'less than' condition.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <typeparam name="TValue">The value type of the property.</typeparam>
        /// <param name="builder">The WhereGroupBuilder instance.</param>
        /// <param name="fieldSelector">The lambda expression selecting the field.</param>
        /// <param name="value">The value to compare against.</param>
        /// <returns>This WhereGroupBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when builder or fieldSelector is null.</exception>
        public static WhereGroupBuilder<TEntity> WhereLessThan<TEntity, TValue>(
            this WhereGroupBuilder<TEntity> builder,
            Expression<Func<TEntity, TValue>> fieldSelector,
            TValue value)
            where TEntity : Entity
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.Where(fieldSelector, ConditionOperator.LessThan, value);
        }

        /// <summary>
        /// Adds a 'null' condition.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <typeparam name="TValue">The value type of the property.</typeparam>
        /// <param name="builder">The WhereGroupBuilder instance.</param>
        /// <param name="fieldSelector">The lambda expression selecting the field.</param>
        /// <returns>This WhereGroupBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when builder or fieldSelector is null.</exception>
        public static WhereGroupBuilder<TEntity> WhereNull<TEntity, TValue>(
            this WhereGroupBuilder<TEntity> builder,
            Expression<Func<TEntity, TValue>> fieldSelector)
            where TEntity : Entity
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.Where(fieldSelector, ConditionOperator.Null);
        }

        /// <summary>
        /// Adds a 'not null' condition.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <typeparam name="TValue">The value type of the property.</typeparam>
        /// <param name="builder">The WhereGroupBuilder instance.</param>
        /// <param name="fieldSelector">The lambda expression selecting the field.</param>
        /// <returns>This WhereGroupBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when builder or fieldSelector is null.</exception>
        public static WhereGroupBuilder<TEntity> WhereNotNull<TEntity, TValue>(
            this WhereGroupBuilder<TEntity> builder,
            Expression<Func<TEntity, TValue>> fieldSelector)
            where TEntity : Entity
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.Where(fieldSelector, ConditionOperator.NotNull);
        }
    }
}
