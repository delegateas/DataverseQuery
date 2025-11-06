using DataverseQuery.QueryBuilder.Interfaces;
using DataverseQuery.QueryBuilder.Services;
using Microsoft.Xrm.Sdk;

namespace DataverseQuery.QueryBuilder
{
    /// <summary>
    /// Builder for creating strongly-typed projections from query results.
    /// Use this with the source-generated result types for compile-time safety.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    public sealed class ProjectionBuilder<TEntity> where TEntity : Entity
    {
        private readonly QueryExpressionBuilder<TEntity> queryBuilder;
        private readonly IAttributeNameResolver attributeNameResolver;

        internal ProjectionBuilder(QueryExpressionBuilder<TEntity> queryBuilder, IAttributeNameResolver attributeNameResolver)
        {
            this.queryBuilder = queryBuilder;
            this.attributeNameResolver = attributeNameResolver;
        }

        /// <summary>
        /// Creates a mapper function that projects entities to the specified result type.
        /// This method works with source-generated result types for compile-time safety.
        /// </summary>
        /// <typeparam name="TResult">The result type to project into. Should be a source-generated type.</typeparam>
        /// <param name="projection">Function that projects using QueryProjection.</param>
        /// <returns>A function that maps Entity to TResult.</returns>
        public Func<Entity, TResult> To<TResult>(Func<QueryProjection<TEntity>, TResult> projection)
        {
            return queryBuilder.GetResultMapper(projection);
        }

        /// <summary>
        /// Gets the query expression for execution.
        /// </summary>
        /// <returns>The built QueryExpression.</returns>
        public Microsoft.Xrm.Sdk.Query.QueryExpression Build()
        {
            return queryBuilder.Build();
        }

        /// <summary>
        /// Gets the underlying query builder for advanced scenarios.
        /// </summary>
        /// <returns>The QueryExpressionBuilder instance.</returns>
        public QueryExpressionBuilder<TEntity> GetQueryBuilder()
        {
            return queryBuilder;
        }
    }
}
