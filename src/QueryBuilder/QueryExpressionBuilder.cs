using System.Linq.Expressions;
using DataverseQuery.QueryBuilder.Interfaces;
using DataverseQuery.QueryBuilder.Services;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseQuery
{
    public sealed class QueryExpressionBuilder<TEntity> : IQueryBuilder
        where TEntity : Entity
    {
        private readonly string entityLogicalName;
        private readonly List<string> columns = new();
        private readonly List<FilterExpression> filters = new();
        private readonly List<ExpandBuilder> expands = new();
        private readonly IAttributeNameResolver attributeNameResolver;
        private readonly IValueConverter valueConverter;
        private int? topCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryExpressionBuilder{TEntity}"/> class.
        /// </summary>
        public QueryExpressionBuilder()
        {
            var logicalNameProp = typeof(TEntity).GetField("EntityLogicalName");
            entityLogicalName = logicalNameProp?.GetValue(null) as string
                ?? typeof(TEntity).Name.ToLowerInvariant();

            attributeNameResolver = new AttributeNameResolver();
            valueConverter = new ValueConverter();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryExpressionBuilder{TEntity}"/> class with custom services.
        /// </summary>
        /// <param name="attributeNameResolver">The service for resolving attribute names from expressions.</param>
        /// <param name="valueConverter">The service for converting values for Dataverse queries.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        internal QueryExpressionBuilder(
            IAttributeNameResolver attributeNameResolver,
            IValueConverter valueConverter)
        {
            var logicalNameProp = typeof(TEntity).GetField("EntityLogicalName");
            entityLogicalName = logicalNameProp?.GetValue(null) as string
                ?? typeof(TEntity).Name.ToLowerInvariant();

            this.attributeNameResolver = attributeNameResolver ?? throw new ArgumentNullException(nameof(attributeNameResolver));
            this.valueConverter = valueConverter ?? throw new ArgumentNullException(nameof(valueConverter));
        }

        // Implement interface methods
        public ColumnSet GetColumns()
        {
            return columns.Count > 0
                ? new ColumnSet(columns.ToArray())
                : new ColumnSet(true);
        }

        public FilterExpression GetCombinedFilter()
        {
            if (filters.Count == 0) return new FilterExpression();

            var filter = new FilterExpression(LogicalOperator.And);
            foreach (var f in filters)
            {
                filter.AddFilter(f);
            }

            return filter;
        }

        public IEnumerable<ExpandBuilder> GetExpands()
        {
            return expands.AsReadOnly();
        }

        public QueryExpressionBuilder<TEntity> Select(params Expression<Func<TEntity, object>>[] selectors)
        {
            ArgumentNullException.ThrowIfNull(selectors);

            foreach (var selector in selectors)
            {
                var name = attributeNameResolver.GetAttributeName<TEntity, object>(selector);
                if (!string.IsNullOrEmpty(name))
                {
                    columns.Add(name);
                }
            }

            return this;
        }

        public QueryExpressionBuilder<TEntity> Where<TValue>(
            Expression<Func<TEntity, TValue>> fieldSelector,
            ConditionOperator op,
            params TValue[] values)
        {
            ArgumentNullException.ThrowIfNull(fieldSelector);
            ArgumentNullException.ThrowIfNull(values);

            var name = attributeNameResolver.GetAttributeName(fieldSelector);
            if (!string.IsNullOrEmpty(name))
            {
                var andFilter = GetOrCreateAndFilter();
                var primitiveValues = valueConverter.ConvertValues(values);
                andFilter.AddCondition(name, op, primitiveValues);
            }

            return this;
        }

        public QueryExpressionBuilder<TEntity> Expand<TTarget>(
            Expression<Func<TEntity, IEnumerable<TTarget>>> navigation,
            Action<QueryExpressionBuilder<TTarget>> configure)
            where TTarget : Entity
        {
            ArgumentNullException.ThrowIfNull(navigation);
            ArgumentNullException.ThrowIfNull(configure);

            var relName = GetRelationshipSchemaName(navigation);
            if (!string.IsNullOrEmpty(relName))
            {
                var childBuilder = new QueryExpressionBuilder<TTarget>();
                configure.Invoke(childBuilder);
                expands.Add(new ExpandBuilder(
                    relName,
                    typeof(TTarget),
                    childBuilder,
                    true));
            }

            return this;
        }

        public QueryExpressionBuilder<TEntity> Expand<TTarget>(
            Expression<Func<TEntity, TTarget>> navigation,
            Action<QueryExpressionBuilder<TTarget>> configure)
            where TTarget : Entity
        {
            ArgumentNullException.ThrowIfNull(navigation);
            ArgumentNullException.ThrowIfNull(configure);

            var relName = GetRelationshipSchemaName(navigation);
            if (!string.IsNullOrEmpty(relName))
            {
                var childBuilder = new QueryExpressionBuilder<TTarget>();
                configure.Invoke(childBuilder);
                expands.Add(new ExpandBuilder(
                    relName,
                    typeof(TTarget),
                    childBuilder,
                    false));
            }

            return this;
        }

        public QueryExpressionBuilder<TEntity> Top(int count)
        {
            topCount = count;
            return this;
        }

        public QueryExpression Build()
        {
            var qe = new QueryExpression(entityLogicalName)
            {
                ColumnSet = columns.Count > 0 ? new ColumnSet(columns.ToArray()) : new ColumnSet(true),
            };

            if (filters.Count > 0)
            {
                var filter = new FilterExpression(LogicalOperator.And);
                foreach (var f in filters)
                {
                    filter.AddFilter(f);
                }

                qe.Criteria = filter;
            }

            foreach (var expand in expands)
            {
                var link = BuildLinkEntity(expand);
                qe.LinkEntities.Add(link);
            }

            if (topCount.HasValue)
            {
                qe.TopCount = topCount;
            }

            return qe;
        }

        public LinkEntity BuildLinkEntity(ExpandBuilder expand)
        {
            ArgumentNullException.ThrowIfNull(expand);

            var (fromAttr, toAttr) = GetLinkAttributes(expand);

            var link = new LinkEntity
            {
                JoinOperator = JoinOperator.Inner,
                LinkToEntityName = expand.RelationshipName,
                LinkFromEntityName = entityLogicalName,
                LinkFromAttributeName = fromAttr,
                LinkToAttributeName = toAttr,
            };

            var expandBuilder = expand.Builder;
            link.Columns = expandBuilder.GetColumns();
            link.LinkCriteria = expandBuilder.GetCombinedFilter();

            // Build nested expands using interface
            foreach (var childExpand in expandBuilder.GetExpands())
            {
                var childLink = expandBuilder.BuildLinkEntity(childExpand);
                link.LinkEntities.Add(childLink);
            }

            return link;
        }

        private static (string FromAttr, string ToAttr) GetLinkAttributes(ExpandBuilder expand)
        {
            if (expand.IsCollection)
            {
                return GetLinkAttributesForCollection(expand.RelationshipName);
            }
            else if (expand.TargetType == typeof(TEntity))
            {
                var fromAttr = expand.RelationshipName + "id";
                var toAttr = expand.RelationshipName + "id";
                return (fromAttr, toAttr);
            }
            else
            {
                return GetLinkAttributesForReference(expand.RelationshipName, typeof(TEntity), expand.TargetType);
            }
        }

        private static string GetEntityLogicalName(Type t)
        {
            var logicalNameProp = t.GetField("EntityLogicalName");
            return logicalNameProp?.GetValue(null) as string ?? t.Name.ToLowerInvariant();
        }

        // --- Helpers ---
        private static string? GetRelationshipSchemaName<TTarget>(Expression<Func<TEntity, TTarget>> navigation)
        {
            string? propName = null;
            if (navigation.Body is MemberExpression member)
            {
                propName = member.Member.Name;
            }
            else if (navigation.Body is UnaryExpression unary && unary.Operand is MemberExpression m)
            {
                propName = m.Member.Name;
            }
            else if (navigation.Body is MethodCallExpression methodCall &&
                methodCall.Method.Name == "GetRelatedEntity" && methodCall.Arguments.Count > 0 &&
                methodCall.Arguments[0] is ConstantExpression constExpr &&
                constExpr.Value is string s)
            {
                propName = s;
            }

            if (string.IsNullOrEmpty(propName))
                return null;

#pragma warning disable S6602
            var prop = typeof(TEntity).GetProperties()
                .FirstOrDefault(p => p.Name.Equals(propName, StringComparison.OrdinalIgnoreCase));
#pragma warning restore S6602
            if (prop != null)
            {
#pragma warning disable S6602
                var attr = prop.GetCustomAttributes(false)
                    .FirstOrDefault(a => a.GetType().Name == "RelationshipSchemaNameAttribute");
#pragma warning restore S6602
                var schemaNameProp = attr != null ? attr.GetType().GetProperty("SchemaName") : null;
                var schemaName = schemaNameProp != null ? schemaNameProp.GetValue(attr) as string : null;
                if (!string.IsNullOrEmpty(schemaName))
                    return schemaName;
            }

            return propName;
        }

        private static string? GetRelationshipSchemaName<TTarget>(Expression<Func<TEntity, IEnumerable<TTarget>>> navigation)
        {
            string? propName = null;
            if (navigation.Body is MemberExpression member)
            {
                propName = member.Member.Name;
            }
            else if (navigation.Body is UnaryExpression unary && unary.Operand is MemberExpression m)
            {
                propName = m.Member.Name;
            }

            if (string.IsNullOrEmpty(propName))
                return null;

#pragma warning disable S6602
            var prop = typeof(TEntity).GetProperties()
                .FirstOrDefault(p => p.Name.Equals(propName, StringComparison.OrdinalIgnoreCase));
#pragma warning restore S6602
            if (prop != null)
            {
#pragma warning disable S6602
                var attr = prop.GetCustomAttributes(false)
                    .FirstOrDefault(a => a.GetType().Name == "RelationshipSchemaNameAttribute");
#pragma warning restore S6602
                if (attr != null)
                {
                    var schemaNameProp = attr.GetType().GetProperty("SchemaName");
                    var schemaName = schemaNameProp?.GetValue(attr) as string;
                    if (!string.IsNullOrEmpty(schemaName))
                        return schemaName;
                }
            }

            return propName;
        }

        // For 1:N (collection) navigation property
        private static (string FromAttr, string ToAttr) GetLinkAttributesForCollection(string relName)
        {
            var fromAttr = relName + "id";
            var toAttr = relName + "id";
            return (fromAttr, toAttr);
        }

        // For N:1 (reference) navigation property
        private static (string FromAttr, string ToAttr) GetLinkAttributesForReference(string relName, Type sourceType, Type targetType)
        {
            var prop = sourceType.GetProperty(relName);
#pragma warning disable S6602
            var attr = prop?.GetCustomAttributes(false)
                .FirstOrDefault(a => a.GetType().Name == "AttributeLogicalNameAttribute");
#pragma warning restore S6602
            string? fromAttr = null;
            if (attr is not null)
            {
                var propInfo = attr.GetType().GetProperty("LogicalName");
                fromAttr = propInfo?.GetValue(attr) as string;
            }

            if (string.IsNullOrEmpty(fromAttr))
            {
                fromAttr = relName + "id";
            }

            var toAttr = GetEntityLogicalName(targetType) + "id";
            return (fromAttr, toAttr);
        }

        /// <summary>
        /// Adds a grouped set of filter conditions with the specified logical operator.
        /// </summary>
        /// <param name="logicalOperator">The logical operator to use within the group (And/Or).</param>
        /// <param name="configure">Action to configure the filter group.</param>
        /// <returns>This QueryExpressionBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when configure is null.</exception>
        public QueryExpressionBuilder<TEntity> WhereGroup(
            LogicalOperator logicalOperator,
            Action<WhereGroupBuilder<TEntity>> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var groupBuilder = new WhereGroupBuilder<TEntity>(attributeNameResolver, valueConverter);
            configure(groupBuilder);

            if (groupBuilder.HasConditions)
            {
                var groupFilter = new FilterExpression(logicalOperator);
                foreach (var condition in groupBuilder.GetConditions())
                {
                    groupFilter.AddCondition(condition);
                }

                filters.Add(groupFilter);
            }

            return this;
        }

        /// <summary>
        /// Adds an OR group of filter conditions.
        /// </summary>
        /// <param name="configure">Action to configure the filter group.</param>
        /// <returns>This QueryExpressionBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when configure is null.</exception>
        public QueryExpressionBuilder<TEntity> OrWhereGroup(Action<WhereGroupBuilder<TEntity>> configure)
            => WhereGroup(LogicalOperator.Or, configure);

        /// <summary>
        /// Adds an AND group of filter conditions.
        /// </summary>
        /// <param name="configure">Action to configure the filter group.</param>
        /// <returns>This QueryExpressionBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when configure is null.</exception>
        public QueryExpressionBuilder<TEntity> AndWhereGroup(Action<WhereGroupBuilder<TEntity>> configure)
            => WhereGroup(LogicalOperator.And, configure);

        /// <summary>
        /// Adds multiple OR conditions to a single filter group.
        /// Convenience method for creating OR groups.
        /// </summary>
        /// <param name="configure">Action to configure the filter group.</param>
        /// <returns>This QueryExpressionBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when configure is null.</exception>
        public QueryExpressionBuilder<TEntity> WhereAny(Action<WhereGroupBuilder<TEntity>> configure)
            => OrWhereGroup(configure);

        /// <summary>
        /// Adds multiple AND conditions to a single filter group.
        /// Convenience method for creating AND groups.
        /// </summary>
        /// <param name="configure">Action to configure the filter group.</param>
        /// <returns>This QueryExpressionBuilder instance for method chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when configure is null.</exception>
        public QueryExpressionBuilder<TEntity> WhereAll(Action<WhereGroupBuilder<TEntity>> configure)
            => AndWhereGroup(configure);

        private FilterExpression GetOrCreateAndFilter()
        {
            var andFilter = filters.LastOrDefault(f => f.FilterOperator == LogicalOperator.And);
            if (andFilter == null)
            {
                andFilter = new FilterExpression(LogicalOperator.And);
                filters.Add(andFilter);
            }

            return andFilter;
        }
    }
}
