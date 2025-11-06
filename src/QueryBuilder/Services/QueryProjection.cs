using System.Linq.Expressions;
using DataverseQuery.QueryBuilder.Extensions;
using DataverseQuery.QueryBuilder.Interfaces;
using Microsoft.Xrm.Sdk;

namespace DataverseQuery.QueryBuilder.Services
{
    /// <summary>
    /// Provides type-safe methods for projecting Entity data into strongly-typed objects.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    public class QueryProjection<TEntity> where TEntity : Entity
    {
        private readonly Entity entity;
        private readonly Dictionary<string, string> aliasMap;
        private readonly IAttributeNameResolver attributeNameResolver;

        internal QueryProjection(Entity entity, Dictionary<string, string> aliasMap, IAttributeNameResolver attributeNameResolver)
        {
            this.entity = entity;
            this.aliasMap = aliasMap;
            this.attributeNameResolver = attributeNameResolver;
        }

        /// <summary>
        /// Gets a value from the main entity using a strongly-typed expression.
        /// </summary>
        /// <typeparam name="TValue">The type of the value to retrieve.</typeparam>
        /// <param name="selector">Expression selecting the property from the entity.</param>
        /// <returns>The attribute value, or default if not found.</returns>
        public TValue? Get<TValue>(Expression<Func<TEntity, TValue>> selector)
        {
            var attributeName = attributeNameResolver.GetAttributeName(selector);
            if (string.IsNullOrEmpty(attributeName))
            {
                return default;
            }

            return entity.GetAttributeValue<TValue>(attributeName);
        }

        /// <summary>
        /// Projects a linked entity into a strongly-typed object.
        /// </summary>
        /// <typeparam name="TTarget">The target entity type.</typeparam>
        /// <typeparam name="TResult">The result type to project into.</typeparam>
        /// <param name="navigation">Expression selecting the navigation property.</param>
        /// <param name="projection">Function to project the linked entity.</param>
        /// <returns>The projected result, or default if the linked entity is not present.</returns>
        public TResult? GetLinked<TTarget, TResult>(
            Expression<Func<TEntity, TTarget>> navigation,
            Func<LinkedEntityProjection, TResult> projection)
            where TTarget : Entity
        {
            var relationshipName = GetRelationshipName(navigation);
            if (string.IsNullOrEmpty(relationshipName) || !aliasMap.TryGetValue(relationshipName, out var alias))
            {
                return default;
            }

            var linkedProjection = new LinkedEntityProjection(entity, alias, attributeNameResolver);
            if (!linkedProjection.HasValues())
            {
                return default;
            }

            return projection(linkedProjection);
        }

        /// <summary>
        /// Projects a linked entity collection into a strongly-typed object.
        /// Note: This returns a single object since Dataverse queries flatten results.
        /// </summary>
        /// <typeparam name="TTarget">The target entity type.</typeparam>
        /// <typeparam name="TResult">The result type to project into.</typeparam>
        /// <param name="navigation">Expression selecting the navigation property.</param>
        /// <param name="projection">Function to project the linked entity.</param>
        /// <returns>The projected result, or default if the linked entity is not present.</returns>
        public TResult? GetLinked<TTarget, TResult>(
            Expression<Func<TEntity, IEnumerable<TTarget>>> navigation,
            Func<LinkedEntityProjection, TResult> projection)
            where TTarget : Entity
        {
            var relationshipName = GetRelationshipName(navigation);
            if (string.IsNullOrEmpty(relationshipName) || !aliasMap.TryGetValue(relationshipName, out var alias))
            {
                return default;
            }

            var linkedProjection = new LinkedEntityProjection(entity, alias, attributeNameResolver);
            if (!linkedProjection.HasValues())
            {
                return default;
            }

            return projection(linkedProjection);
        }

        private string? GetRelationshipName<TTarget>(Expression<Func<TEntity, TTarget>> navigation)
        {
            if (navigation.Body is MemberExpression member)
            {
                return member.Member.Name;
            }
            else if (navigation.Body is UnaryExpression unary && unary.Operand is MemberExpression unaryMember)
            {
                return unaryMember.Member.Name;
            }

            return null;
        }

        private string? GetRelationshipName<TTarget>(Expression<Func<TEntity, IEnumerable<TTarget>>> navigation)
        {
            if (navigation.Body is MemberExpression member)
            {
                return member.Member.Name;
            }
            else if (navigation.Body is UnaryExpression unary && unary.Operand is MemberExpression unaryMember)
            {
                return unaryMember.Member.Name;
            }

            return null;
        }
    }

    /// <summary>
    /// Provides type-safe methods for projecting linked entity data.
    /// </summary>
    public class LinkedEntityProjection
    {
        private readonly Entity entity;
        private readonly string alias;
        private readonly IAttributeNameResolver attributeNameResolver;
        private readonly Dictionary<string, string> nestedAliasMap;

        internal LinkedEntityProjection(Entity entity, string alias, IAttributeNameResolver attributeNameResolver)
        {
            this.entity = entity;
            this.alias = alias;
            this.attributeNameResolver = attributeNameResolver;
            this.nestedAliasMap = new Dictionary<string, string>();
        }

        internal LinkedEntityProjection(Entity entity, string alias, IAttributeNameResolver attributeNameResolver, Dictionary<string, string> nestedAliasMap)
        {
            this.entity = entity;
            this.alias = alias;
            this.attributeNameResolver = attributeNameResolver;
            this.nestedAliasMap = nestedAliasMap;
        }

        /// <summary>
        /// Gets a value from the linked entity using a strongly-typed expression.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <typeparam name="TValue">The type of the value to retrieve.</typeparam>
        /// <param name="selector">Expression selecting the property from the entity.</param>
        /// <returns>The attribute value, or default if not found.</returns>
        public TValue? Get<TEntity, TValue>(Expression<Func<TEntity, TValue>> selector)
            where TEntity : Entity
        {
            var attributeName = attributeNameResolver.GetAttributeName(selector);
            if (string.IsNullOrEmpty(attributeName))
            {
                return default;
            }

            return entity.GetAliasedValue<TValue>(alias, attributeName);
        }

        /// <summary>
        /// Projects a nested linked entity into a strongly-typed object.
        /// </summary>
        /// <typeparam name="TEntity">The parent entity type.</typeparam>
        /// <typeparam name="TTarget">The target entity type.</typeparam>
        /// <typeparam name="TResult">The result type to project into.</typeparam>
        /// <param name="navigation">Expression selecting the navigation property.</param>
        /// <param name="projection">Function to project the linked entity.</param>
        /// <returns>The projected result, or default if the linked entity is not present.</returns>
        public TResult? GetLinked<TEntity, TTarget, TResult>(
            Expression<Func<TEntity, TTarget>> navigation,
            Func<LinkedEntityProjection, TResult> projection)
            where TEntity : Entity
            where TTarget : Entity
        {
            var relationshipName = GetRelationshipName(navigation);
            if (string.IsNullOrEmpty(relationshipName) || !nestedAliasMap.TryGetValue(relationshipName, out var nestedAlias))
            {
                return default;
            }

            var linkedProjection = new LinkedEntityProjection(entity, nestedAlias, attributeNameResolver, nestedAliasMap);
            if (!linkedProjection.HasValues())
            {
                return default;
            }

            return projection(linkedProjection);
        }

        /// <summary>
        /// Projects a nested linked entity collection into a strongly-typed object.
        /// </summary>
        /// <typeparam name="TEntity">The parent entity type.</typeparam>
        /// <typeparam name="TTarget">The target entity type.</typeparam>
        /// <typeparam name="TResult">The result type to project into.</typeparam>
        /// <param name="navigation">Expression selecting the navigation property.</param>
        /// <param name="projection">Function to project the linked entity.</param>
        /// <returns>The projected result, or default if the linked entity is not present.</returns>
        public TResult? GetLinked<TEntity, TTarget, TResult>(
            Expression<Func<TEntity, IEnumerable<TTarget>>> navigation,
            Func<LinkedEntityProjection, TResult> projection)
            where TEntity : Entity
            where TTarget : Entity
        {
            var relationshipName = GetRelationshipName(navigation);
            if (string.IsNullOrEmpty(relationshipName) || !nestedAliasMap.TryGetValue(relationshipName, out var nestedAlias))
            {
                return default;
            }

            var linkedProjection = new LinkedEntityProjection(entity, nestedAlias, attributeNameResolver, nestedAliasMap);
            if (!linkedProjection.HasValues())
            {
                return default;
            }

            return projection(linkedProjection);
        }

        internal void AddNestedAlias(string relationshipName, string nestedAlias)
        {
            nestedAliasMap[relationshipName] = nestedAlias;
        }

        internal bool HasValues()
        {
            // Check if any attribute with this alias exists in the entity
            return entity.Attributes.Keys.Any(key => key.StartsWith($"{alias}."));
        }

        private string? GetRelationshipName<TEntity, TTarget>(Expression<Func<TEntity, TTarget>> navigation)
            where TEntity : Entity
        {
            if (navigation.Body is MemberExpression member)
            {
                return member.Member.Name;
            }
            else if (navigation.Body is UnaryExpression unary && unary.Operand is MemberExpression unaryMember)
            {
                return unaryMember.Member.Name;
            }

            return null;
        }

        private string? GetRelationshipName<TEntity, TTarget>(Expression<Func<TEntity, IEnumerable<TTarget>>> navigation)
            where TEntity : Entity
        {
            if (navigation.Body is MemberExpression member)
            {
                return member.Member.Name;
            }
            else if (navigation.Body is UnaryExpression unary && unary.Operand is MemberExpression unaryMember)
            {
                return unaryMember.Member.Name;
            }

            return null;
        }
    }
}
