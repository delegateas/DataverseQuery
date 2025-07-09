using System.Linq.Expressions;
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
        private int? topCount;

        public QueryExpressionBuilder()
        {
            var logicalNameProp = typeof(TEntity).GetField("EntityLogicalName");
            entityLogicalName = logicalNameProp?.GetValue(null) as string
                ?? typeof(TEntity).Name.ToLowerInvariant();
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
            if (filters.Count == 0)
                return new FilterExpression();

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
                var name = GetAttributeName<object>(selector);
                if (!string.IsNullOrEmpty(name))
                {
                    columns.Add(name.ToLowerInvariant());
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

            var name = GetAttributeName(fieldSelector);
            if (!string.IsNullOrEmpty(name))
            {
                var filter = new FilterExpression();
                var primitiveValues = values
                    .Cast<object>()
                    .Select(v =>
                        v switch
                        {
                            Enum => Convert.ChangeType(v, Enum.GetUnderlyingType(v.GetType()), System.Globalization.CultureInfo.InvariantCulture),
                            EntityReference er => er.Id,
                            _ => v,
                        })
                    .ToArray();
                filter.AddCondition(name.ToLowerInvariant(), op, primitiveValues);
                filters.Add(filter);
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
        private static string? GetAttributeName<TValue>(Expression<Func<TEntity, TValue>> expr)
        {
            if (expr.Body is MemberExpression member)
            {
                return member.Member.Name;
            }

            if (expr.Body is UnaryExpression unary && unary.Operand is MemberExpression m)
            {
                return m.Member.Name;
            }

            return null;
        }

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
    }
}