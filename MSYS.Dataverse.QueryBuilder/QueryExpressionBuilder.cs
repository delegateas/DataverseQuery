using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseQuery
{
    /// <summary>
    /// Strongly-typed QueryExpression builder for Dataverse entities.
    /// </summary>
    /// <typeparam name="TEntity">Entity type (e.g., Account, Contact)</typeparam>
    public class QueryExpressionBuilder<TEntity>
        where TEntity : Entity
    {
        private readonly string _entityLogicalName;
        private readonly List<string> _columns = new();
        private readonly List<FilterExpression> _filters = new();
        private readonly List<ExpandBuilder> _expands = new();
        private int? _topCount;

        private class ExpandBuilder
        {
            public string RelationshipName { get; set; }
            public Type TargetType { get; set; }
            public object Builder { get; set; }
            public bool IsCollection { get; set; }
        }

        public QueryExpressionBuilder()
        {
            // Use static property if available, else fallback to reflection
            var logicalNameProp = typeof(TEntity).GetField("EntityLogicalName");
            _entityLogicalName = logicalNameProp?.GetValue(null) as string
                ?? typeof(TEntity).Name.ToLowerInvariant();
        }

        /// <summary>
        /// Selects columns using strongly-typed property expressions.
        /// </summary>
        public QueryExpressionBuilder<TEntity> Select(params Expression<Func<TEntity, object>>[] selectors)
        {
            foreach (var selector in selectors)
            {
                var name = GetAttributeName(selector);
                if (!string.IsNullOrEmpty(name))
                    _columns.Add(name);
            }
            return this;
        }

        /// <summary>
        /// Adds a filter to the query.
        /// </summary>
        public QueryExpressionBuilder<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
        {
            var filter = ExpressionToFilter(predicate);
            if (filter != null)
                _filters.Add(filter);
            return this;
        }

        /// <summary>
        /// Expands a collection (1:N) relationship (navigation property) with a strongly-typed builder for the target.
        /// </summary>
        public QueryExpressionBuilder<TEntity> Expand<TTarget>(
            Expression<Func<TEntity, IEnumerable<TTarget>>> navigation,
            Action<QueryExpressionBuilder<TTarget>> configure)
            where TTarget : Entity
        {
            var relName = GetRelationshipName<TTarget>(navigation);
            if (!string.IsNullOrEmpty(relName))
            {
                var childBuilder = new QueryExpressionBuilder<TTarget>();
                configure?.Invoke(childBuilder);
                _expands.Add(new ExpandBuilder
                {
                    RelationshipName = relName,
                    TargetType = typeof(TTarget),
                    Builder = childBuilder,
                    IsCollection = true
                });
            }
            return this;
        }

        /// <summary>
        /// Expands a single (N:1) relationship (navigation property) with a strongly-typed builder for the target.
        /// </summary>
        public QueryExpressionBuilder<TEntity> Expand<TTarget>(
            Expression<Func<TEntity, TTarget>> navigation,
            Action<QueryExpressionBuilder<TTarget>> configure)
            where TTarget : Entity
        {
            var relName = GetRelationshipName<TTarget>(navigation);
            if (!string.IsNullOrEmpty(relName))
            {
                var childBuilder = new QueryExpressionBuilder<TTarget>();
                configure?.Invoke(childBuilder);
                _expands.Add(new ExpandBuilder
                {
                    RelationshipName = relName,
                    TargetType = typeof(TTarget),
                    Builder = childBuilder,
                    IsCollection = false
                });
            }
            return this;
        }

        /// <summary>
        /// Limits the number of returned records.
        /// </summary>
        public QueryExpressionBuilder<TEntity> Top(int count)
        {
            _topCount = count;
            return this;
        }

        /// <summary>
        /// Builds the QueryExpression, including all nested expands.
        /// </summary>
        public QueryExpression Build()
        {
            var qe = new QueryExpression(_entityLogicalName)
            {
                ColumnSet = _columns.Count > 0 ? new ColumnSet(_columns.ToArray()) : new ColumnSet(true)
            };

            if (_filters.Count > 0)
            {
                var filter = new FilterExpression(LogicalOperator.And);
                foreach (var f in _filters)
                    filter.AddFilter(f);
                qe.Criteria = filter;
            }

            foreach (var expand in _expands)
            {
                var link = BuildLinkEntity(expand);
                qe.LinkEntities.Add(link);
            }

            if (_topCount.HasValue)
                qe.TopCount = _topCount;

            return qe;
        }

        private LinkEntity BuildLinkEntity(ExpandBuilder expand)
        {
            string fromAttr, toAttr;
            if (expand.IsCollection)
            {
                var method = typeof(QueryExpressionBuilder<TEntity>)
                    .GetMethod(nameof(GetLinkAttributesForCollection), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .MakeGenericMethod(typeof(TEntity), expand.TargetType);
                (fromAttr, toAttr) = ((string, string))method.Invoke(null, new object[] { expand.RelationshipName });
            }
            else
            {
                var method = typeof(QueryExpressionBuilder<TEntity>)
                    .GetMethod(nameof(GetLinkAttributesForReference), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .MakeGenericMethod(typeof(TEntity), expand.TargetType);
                (fromAttr, toAttr) = ((string, string))method.Invoke(null, new object[] { expand.RelationshipName });
            }

            var link = new LinkEntity
            {
                JoinOperator = JoinOperator.Inner,
                LinkToEntityName = GetEntityLogicalName(expand.TargetType),
                LinkFromEntityName = _entityLogicalName,
                LinkFromAttributeName = fromAttr,
                LinkToAttributeName = toAttr
            };

            // Set columns and filters for the child builder
            var childBuilder = expand.Builder;
            var columnsProp = childBuilder.GetType().GetField("_columns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var columns = (List<string>)columnsProp.GetValue(childBuilder);
            if (columns.Count > 0)
                link.Columns = new ColumnSet(columns.ToArray());
            else
                link.Columns = new ColumnSet(true);

            var filtersProp = childBuilder.GetType().GetField("_filters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var filters = (List<FilterExpression>)filtersProp.GetValue(childBuilder);
            if (filters.Count > 0)
            {
                var filter = new FilterExpression(LogicalOperator.And);
                foreach (var f in filters)
                    filter.AddFilter(f);
                link.LinkCriteria = filter;
            }

            // Recursively add child expands
            var expandsProp = childBuilder.GetType().GetField("_expands", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var expands = (List<ExpandBuilder>)expandsProp.GetValue(childBuilder);
            foreach (var childExpand in expands)
            {
                var childLink = (LinkEntity)childBuilder.GetType().GetMethod("BuildLinkEntity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(childBuilder, new object[] { childExpand });
                link.LinkEntities.Add(childLink);
            }

            return link;
        }

        private static string GetEntityLogicalName(Type t)
        {
            var logicalNameProp = t.GetField("EntityLogicalName");
            return logicalNameProp?.GetValue(null) as string ?? t.Name.ToLowerInvariant();
        }

        // --- Helpers ---

        private static string GetAttributeName(Expression<Func<TEntity, object>> expr)
        {
            if (expr.Body is MemberExpression member)
                return member.Member.Name;
            if (expr.Body is UnaryExpression unary && unary.Operand is MemberExpression m)
                return m.Member.Name;
            return null;
        }

        private static string GetRelationshipName<TTarget>(Expression expr)
        {
            // Handles both IEnumerable<TTarget> and TTarget navigation properties
            if (expr is LambdaExpression lambda)
            {
                if (lambda.Body is MemberExpression member)
                    return member.Member.Name;
                if (lambda.Body is UnaryExpression unary && unary.Operand is MemberExpression m)
                    return m.Member.Name;
            }
            return null;
        }

        // For 1:N (collection) navigation property
        private static (string fromAttr, string toAttr) GetLinkAttributesForCollection<TSource, TTarget>(string relName)
            where TSource : Entity
            where TTarget : Entity
        {
            // Try to get [RelationshipSchemaName] or [AttributeLogicalName] via reflection if available
            // Fallback: assume relName + "id" for both sides (may need adjustment for real-world cases)
            return (relName + "id", relName + "id");
        }

        // For N:1 (reference) navigation property
        private static (string fromAttr, string toAttr) GetLinkAttributesForReference<TSource, TTarget>(string relName)
            where TSource : Entity
            where TTarget : Entity
        {
            // Try to get [AttributeLogicalName] from the property on TSource
            var prop = typeof(TSource).GetProperty(relName);
            var attr = prop?.GetCustomAttributes(false)
                .FirstOrDefault(a => a.GetType().Name == "AttributeLogicalNameAttribute");
            string fromAttr = null;
            if (attr != null)
            {
                var propInfo = attr.GetType().GetProperty("LogicalName");
                fromAttr = propInfo?.GetValue(attr) as string;
            }
            // Fallback: relName + "id"
            if (string.IsNullOrEmpty(fromAttr))
                fromAttr = relName + "id";
            // toAttr is usually the primary key of TTarget, which is TTarget.EntityLogicalName + "id"
            var toAttr = GetEntityLogicalName<TTarget>() + "id";
            return (fromAttr, toAttr);
        }

        private static string GetEntityLogicalName<T>() where T : Entity
        {
            var logicalNameProp = typeof(T).GetField("EntityLogicalName");
            return logicalNameProp?.GetValue(null) as string ?? typeof(T).Name.ToLowerInvariant();
        }

        // This is a stub. For a real implementation, use a library or hand-write for common cases.
        private static FilterExpression ExpressionToFilter(Expression<Func<TEntity, bool>> predicate)
        {
            // Only supports simple equality: e => e.Prop == value
            if (predicate.Body is BinaryExpression be && be.NodeType == ExpressionType.Equal)
            {
                var left = be.Left as MemberExpression;
                var right = be.Right as ConstantExpression;
                if (left != null && right != null)
                {
                    var filter = new FilterExpression();
                    filter.AddCondition(left.Member.Name, ConditionOperator.Equal, right.Value);
                    return filter;
                }
            }
            // Not supported
            throw new NotSupportedException("Only simple equality expressions are supported in Where().");
        }
    }

    public static class QueryExpressionBuilderExtensions
    {
        /// <summary>
        /// Executes the built query and returns the entities.
        /// </summary>
        public static List<TEntity> RetrieveAll<TEntity>(this IOrganizationService service, QueryExpressionBuilder<TEntity> builder)
            where TEntity : Entity, new()
        {
            var qe = builder.Build();
            var result = service.RetrieveMultiple(qe);
            return result.Entities.Select(e => e.ToEntity<TEntity>()).ToList();
        }
    }
}
