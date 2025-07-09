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
    }
}