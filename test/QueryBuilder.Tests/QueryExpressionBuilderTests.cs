using DataverseQuery.QueryBuilder;
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
            Assert.Single(nestedLink.LinkCriteria.Filters);
            var filter = nestedLink.LinkCriteria.Filters[0];
            Assert.Single(filter.Conditions);
            var condition = filter.Conditions[0];
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

        [Fact]
        public void WhereGroup_WithOrConditions_CreatesCorrectFilterStructure()
        {
            // Arrange & Act
            var query = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(a => a.Name, a => a.StateCode)
                .Where(a => a.StateCode, ConditionOperator.Equal, SharedContext.AccountState.Aktiv)
                .OrWhereGroup(group => group
                    .Where(a => a.Name, ConditionOperator.Like, "%Test%")
                    .Where(a => a.AccountNumber, ConditionOperator.Like, "%123%"))
                .Build();

            // Assert
            Assert.Equal(2, query.Criteria.Filters.Count);

            var andFilter = query.Criteria.Filters.First(f => f.FilterOperator == LogicalOperator.And);
            Assert.Single(andFilter.Conditions);
            Assert.Equal("statecode", andFilter.Conditions[0].AttributeName);

            var orFilter = query.Criteria.Filters.First(f => f.FilterOperator == LogicalOperator.Or);
            Assert.Equal(2, orFilter.Conditions.Count);
            Assert.Equal("name", orFilter.Conditions[0].AttributeName);
            Assert.Equal("accountnumber", orFilter.Conditions[1].AttributeName);
        }

        [Fact]
        public void AndWhereGroup_CreatesAndFilter()
        {
            // Arrange & Act
            var query = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(a => a.Name)
                .AndWhereGroup(group => group
                    .Where(a => a.Name, ConditionOperator.Like, "%Test%")
                    .Where(a => a.StateCode, ConditionOperator.Equal, SharedContext.AccountState.Aktiv))
                .Build();

            // Assert
            Assert.Single(query.Criteria.Filters);
            var andFilter = query.Criteria.Filters[0];
            Assert.Equal(LogicalOperator.And, andFilter.FilterOperator);
            Assert.Equal(2, andFilter.Conditions.Count);
        }

        [Fact]
        public void WhereAny_CreatesOrFilter()
        {
            // Arrange & Act
            var query = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(a => a.Name)
                .WhereAny(group => group
                    .Where(a => a.Name, ConditionOperator.Like, "%Test%")
                    .Where(a => a.Name, ConditionOperator.Like, "%Demo%"))
                .Build();

            // Assert
            Assert.Single(query.Criteria.Filters);
            var orFilter = query.Criteria.Filters[0];
            Assert.Equal(LogicalOperator.Or, orFilter.FilterOperator);
            Assert.Equal(2, orFilter.Conditions.Count);
        }

        [Fact]
        public void WhereAll_CreatesAndFilter()
        {
            // Arrange & Act
            var query = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(a => a.Name)
                .WhereAll(group => group
                    .Where(a => a.StateCode, ConditionOperator.Equal, SharedContext.AccountState.Aktiv)
                    .Where(a => a.StatusCode, ConditionOperator.Equal, SharedContext.Account_StatusCode.Aktiv))
                .Build();

            // Assert
            Assert.Single(query.Criteria.Filters);
            var andFilter = query.Criteria.Filters[0];
            Assert.Equal(LogicalOperator.And, andFilter.FilterOperator);
            Assert.Equal(2, andFilter.Conditions.Count);
        }

        [Fact]
        public void ComplexGrouping_WithMultipleFilters_CreatesCorrectStructure()
        {
            // Arrange & Act - (status = active OR status = inactive) AND (type = customer OR type = partner)
            var query = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(a => a.Name)
                .OrWhereGroup(group => group
                    .Where(a => a.StateCode, ConditionOperator.Equal, SharedContext.AccountState.Aktiv)
                    .Where(a => a.StateCode, ConditionOperator.Equal, SharedContext.AccountState.Inaktiv))
                .OrWhereGroup(group => group
                    .Where(a => a.CustomerTypeCode, ConditionOperator.Equal, SharedContext.Account_CustomerTypeCode.Kunde)
                    .Where(a => a.CustomerTypeCode, ConditionOperator.Equal, SharedContext.Account_CustomerTypeCode.Partner))
                .Build();

            // Assert
            Assert.Equal(2, query.Criteria.Filters.Count);

            foreach (var filter in query.Criteria.Filters)
            {
                Assert.Equal(LogicalOperator.Or, filter.FilterOperator);
                Assert.Equal(2, filter.Conditions.Count);
            }
        }

        [Fact]
        public void WhereGroup_WithEmptyGroup_DoesNotAddFilter()
        {
            // Arrange & Act
            var query = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(a => a.Name)
                .Where(a => a.StateCode, ConditionOperator.Equal, SharedContext.AccountState.Aktiv)
                .OrWhereGroup(group => { /* Empty group */ })
                .Build();

            // Assert - Only the regular Where condition should be present
            Assert.Single(query.Criteria.Filters);
            var filter = query.Criteria.Filters[0];
            Assert.Equal(LogicalOperator.And, filter.FilterOperator);
            Assert.Single(filter.Conditions);
        }
    }
}