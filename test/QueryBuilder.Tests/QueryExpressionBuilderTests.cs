using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseQuery.Tests
{
    public class QueryExpressionBuilderTests
    {
        [Fact]
        public void Select_AddsColumns()
        {
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name, e => e.StateCode);

            var query = builder.Build();
            Assert.Contains("Name", query.ColumnSet.Columns);
            Assert.Contains("StateCode", query.ColumnSet.Columns);
            Assert.False(query.ColumnSet.AllColumns);
        }

        [Fact]
        public void Where_AddsSimpleEqualityFilter()
        {
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Where(e => e.StateCode, ConditionOperator.Equal, SharedContext.AccountState.Aktiv);

            var query = builder.Build();
            Assert.NotNull(query.Criteria);
            var filter = query.Criteria.Filters.FirstOrDefault() ?? query.Criteria;
            var condition = filter.Conditions.FirstOrDefault();
            Assert.NotNull(condition);
            Assert.Equal("StateCode", condition.AttributeName);
            Assert.Equal(ConditionOperator.Equal, condition.Operator);
            Assert.Equal((int)SharedContext.AccountState.Aktiv, condition.Values[0]);
        }

        [Fact]
        public void Top_SetsTopCount()
        {
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Top(5);

            var query = builder.Build();
            Assert.Equal(5, query.TopCount);
        }

        [Fact]
        public void Where_EntityReferenceFilter_UsesGuidValue()
        {
            var parentId = Guid.NewGuid();
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Where(e => e.ParentAccountId, ConditionOperator.Equal, new EntityReference(SharedContext.Account.EntityLogicalName, parentId));

            var query = builder.Build();
            var allConditions = query.Criteria.Conditions
                .Concat(query.Criteria.Filters.SelectMany(f => f.Conditions))
                .ToList();
            var condition = allConditions.Find(c => string.Equals(c.AttributeName, "parentaccountid", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(condition);
            Assert.Equal(ConditionOperator.Equal, condition.Operator);
            Assert.IsType<Guid>(condition.Values[0]);
            Assert.Equal(parentId, (Guid)condition.Values[0]);
        }
    }
}
