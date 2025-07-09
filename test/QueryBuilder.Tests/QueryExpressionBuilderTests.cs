using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseQuery.Tests
{
    public class QueryExpressionBuilderTests
    {
        [Fact]
        public void Expand_AddsLinkEntityForCollection()
        {
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Expand(a => a.contact_customer_accounts, b => b.Select(c => c.FirstName, c => c.LastName));

            var query = builder.Build();
            Assert.Single(query.LinkEntities);
            var link = query.LinkEntities[0];
            Assert.Equal("contact_customer_accounts", link.LinkToEntityName);
            Assert.Equal(2, link.Columns.Columns.Count);
            Assert.Contains("firstname", link.Columns.Columns);
            Assert.Contains("lastname", link.Columns.Columns);
        }

        [Fact]
        public void Expand_AddsLinkEntityForReference()
        {
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Expand(a => a.account_primary_contact, b => b.Select(c => c.FirstName, c => c.LastName));

            var query = builder.Build();
            Assert.Single(query.LinkEntities);
            var link = query.LinkEntities[0];
            Assert.Equal("account_primary_contact", link.LinkToEntityName);
            Assert.Equal(2, link.Columns.Columns.Count);
            Assert.Contains("firstname", link.Columns.Columns);
            Assert.Contains("lastname", link.Columns.Columns);
        }

        [Fact]
        public void Expand_NestedExpand_AddsNestedLinkEntities()
        {
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Expand(
                    a => a.Referencingaccount_parent_account,
                    b => b.Expand(
                        c => c.account_primary_contact,
                        d => d.Where(x => x.FirstName, ConditionOperator.NotNull)));

            var query = builder.Build();
            Assert.Single(query.LinkEntities);
            var link = query.LinkEntities[0];
            Assert.Equal("account_parent_account", link.LinkToEntityName);
            Assert.Single(link.LinkEntities);
            var nestedLink = link.LinkEntities[0];
            Assert.Equal("account_primary_contact", nestedLink.LinkToEntityName);
            Assert.NotNull(nestedLink.LinkCriteria);
            Assert.Single(nestedLink.LinkCriteria.Conditions);
            var condition = nestedLink.LinkCriteria.Conditions[0];
            Assert.Equal("firstname", condition.AttributeName);
            Assert.Equal(ConditionOperator.NotNull, condition.Operator);
        }

        [Fact]
        public void Select_AddsColumns()
        {
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name, e => e.StateCode);

            var query = builder.Build();
            Assert.Contains("name", query.ColumnSet.Columns);
            Assert.Contains("statecode", query.ColumnSet.Columns);
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
            Assert.Equal("statecode", condition.AttributeName);
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
