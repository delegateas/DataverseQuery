using System.Linq.Expressions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseQuery
{
    public sealed class QueryExpressionBuilder<TEntity>
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

            var relName = GetRelationshipSchemaName(navigation);
            if (!string.IsNullOrEmpty(relName))
            {
                var childBuilder = new QueryExpressionBuilder<TTarget>();
                configure?.Invoke(childBuilder);
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

            var relName = GetRelationshipSchemaName(navigation);
            if (!string.IsNullOrEmpty(relName))
            {
                var childBuilder = new QueryExpressionBuilder<TTarget>();
                configure?.Invoke(childBuilder);
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

        internal LinkEntity BuildLinkEntity(ExpandBuilder expand)
        {
            var (fromAttr, toAttr) = GetLinkAttributes(expand);

            var link = new LinkEntity
            {
                JoinOperator = JoinOperator.Inner,
                LinkToEntityName = expand.RelationshipName,
                LinkFromEntityName = entityLogicalName,
                LinkFromAttributeName = fromAttr,
                LinkToAttributeName = toAttr,
            };

            var childBuilder = expand.Builder;
            CopyChildColumns(childBuilder, link);
            CopyChildFilters(childBuilder, link);

            // Build nested expands using the correct builder context
#pragma warning disable S3011
            var expandsProp = childBuilder.GetType().GetField("expands", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var childExpands = (List<ExpandBuilder>)expandsProp.GetValue(childBuilder)!;
#pragma warning restore S3011
            foreach (var childExpand in childExpands)
            {
                // Ensure the correct entityLogicalName is used for each builder instance
#pragma warning disable S3011
                var buildLinkMethod = childBuilder.GetType().GetMethod("BuildLinkEntity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
#pragma warning restore S3011
                if (buildLinkMethod == null)
                {
                    throw new InvalidOperationException("BuildLinkEntity method not found on child builder.");
                }

                var childLink = (LinkEntity)buildLinkMethod.Invoke(childBuilder, new object[] { childExpand })!;
                link.LinkEntities.Add(childLink);
            }

            return link;
        }

        private static (string FromAttr, string ToAttr) GetLinkAttributes(ExpandBuilder expand)
        {
            if (expand.IsCollection)
            {
#pragma warning disable S3011
                var method = typeof(QueryExpressionBuilder<TEntity>)
                    .GetMethod(nameof(GetLinkAttributesForCollection), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
                return ((string FromAttr, string ToAttr))method.Invoke(null, new object[] { expand.RelationshipName })!;
#pragma warning restore S3011
            }
            else if (expand.TargetType == typeof(TEntity))
            {
                var fromAttr = expand.RelationshipName + "id";
                var toAttr = expand.RelationshipName + "id";
                return (fromAttr, toAttr);
            }
            else
            {
#pragma warning disable S3011
                var method = typeof(QueryExpressionBuilder<TEntity>)
                    .GetMethod(nameof(GetLinkAttributesForReference), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
                return ((string FromAttr, string ToAttr))method.Invoke(null, new object[] { expand.RelationshipName, typeof(TEntity), expand.TargetType })!;
#pragma warning restore S3011
            }
        }

        private static void CopyChildColumns(object childBuilder, LinkEntity link)
        {
#pragma warning disable S3011
            var columnsProp = childBuilder.GetType().GetField("columns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
#pragma warning restore S3011
            var childColumns = (List<string>)columnsProp.GetValue(childBuilder)!;
            link.Columns = childColumns.Count > 0
                ? new ColumnSet(childColumns.ToArray())
                : new ColumnSet(true);
        }

        private static void CopyChildFilters(object childBuilder, LinkEntity link)
        {
#pragma warning disable S3011
            var filtersProp = childBuilder.GetType().GetField("filters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
#pragma warning restore S3011
            var childFilters = (List<FilterExpression>)filtersProp.GetValue(childBuilder)!;
            if (childFilters.Count > 0)
            {
                var filter = new FilterExpression(LogicalOperator.And);
                foreach (var f in childFilters)
                {
                    filter.AddFilter(f);
                }

                link.LinkCriteria = filter;
            }
        }

        // CopyChildExpands is no longer needed; logic is now in BuildLinkEntity
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

            if (string.IsNullOrEmpty(propName))
                return null;

            var prop = typeof(TEntity).GetProperty(propName);
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

            var prop = typeof(TEntity).GetProperty(propName);
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
