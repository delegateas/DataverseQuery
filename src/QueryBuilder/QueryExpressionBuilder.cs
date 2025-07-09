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

        private sealed class ExpandBuilder
        {
            public string RelationshipName { get; }

            public Type TargetType { get; }

            public object Builder { get; }

            public bool IsCollection { get; }

            public ExpandBuilder(string relationshipName, Type targetType, object builder, bool isCollection)
            {
                RelationshipName = relationshipName;
                TargetType = targetType;
                Builder = builder;
                IsCollection = isCollection;
            }
        }

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

            var name = GetAttributeName(fieldSelector);
            if (!string.IsNullOrEmpty(name))
            {
                var filter = new FilterExpression();
                var primitiveValues = values
                    .Cast<object>()
                    .Select(v => v is Enum ? Convert.ChangeType(v, Enum.GetUnderlyingType(v.GetType()), System.Globalization.CultureInfo.InvariantCulture) : v)
                    .ToArray();
                filter.AddCondition(name, op, primitiveValues);
                filters.Add(filter);
            }

            return this;
        }

        public QueryExpressionBuilder<TEntity> Expand<TTarget>(
            Expression<Func<TEntity, IEnumerable<TTarget>>> navigation,
            Action<QueryExpressionBuilder<TTarget>> configure)
            where TTarget : Entity
        {
            var relName = GetRelationshipName(navigation);
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
            var relName = GetRelationshipName(navigation);
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

        private LinkEntity BuildLinkEntity(ExpandBuilder expand)
        {
            var (fromAttr, toAttr) = GetLinkAttributes(expand);

            var link = new LinkEntity
            {
                JoinOperator = JoinOperator.Inner,
                LinkToEntityName = GetEntityLogicalName(expand.TargetType),
                LinkFromEntityName = entityLogicalName,
                LinkFromAttributeName = fromAttr,
                LinkToAttributeName = toAttr,
            };

            var childBuilder = expand.Builder;
            CopyChildColumns(childBuilder, link);
            CopyChildFilters(childBuilder, link);
            CopyChildExpands(childBuilder, link);

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

        private static void CopyChildExpands(object childBuilder, LinkEntity link)
        {
#pragma warning disable S3011
            var expandsProp = childBuilder.GetType().GetField("expands", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
#pragma warning restore S3011
            var childExpands = (List<ExpandBuilder>)expandsProp.GetValue(childBuilder)!;
            foreach (var childExpand in childExpands)
            {
#pragma warning disable S3011
                var childLink = (LinkEntity)childBuilder.GetType().GetMethod("BuildLinkEntity", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .Invoke(childBuilder, new object[] { childExpand })!;
#pragma warning restore S3011
                link.LinkEntities.Add(childLink);
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

        private static string? GetRelationshipName(Expression expr)
        {
            if (expr is LambdaExpression lambda)
            {
                if (lambda.Body is MemberExpression member)
                {
                    return member.Member.Name;
                }

                if (lambda.Body is UnaryExpression unary && unary.Operand is MemberExpression m)
                {
                    return m.Member.Name;
                }
            }

            return null;
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