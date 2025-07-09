using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using DataverseQuery;
using Xunit;

namespace DataverseQuery.Tests
{
    // Minimal stub for Entity for testing
    public class TestEntity : Entity
    {
        public static string EntityLogicalName => "testentity";
        public string Name { get; set; }
        public int StateCode { get; set; }
        public Guid TestEntityId { get; set; }
    }

    public class QueryExpressionBuilderTests
    {
        [Fact]
        public void Select_AddsColumns()
        {
            var builder = new QueryExpressionBuilder<TestEntity>()
                .Select(e => e.Name, e => e.StateCode);

            var query = builder.Build();
            Assert.Contains("Name", query.ColumnSet.Columns);
            Assert.Contains("StateCode", query.ColumnSet.Columns);
            Assert.False(query.ColumnSet.AllColumns);
        }

        [Fact]
        public void Where_AddsSimpleEqualityFilter()
        {
            var builder = new QueryExpressionBuilder<TestEntity>()
                .Where(e => e.StateCode == 1);

            var query = builder.Build();
            Assert.NotNull(query.Criteria);
            var filter = query.Criteria.Filters.FirstOrDefault() ?? query.Criteria;
            var condition = filter.Conditions.FirstOrDefault();
            Assert.NotNull(condition);
            Assert.Equal("StateCode", condition.AttributeName);
            Assert.Equal(ConditionOperator.Equal, condition.Operator);
            Assert.Equal(1, condition.Values[0]);
        }

        [Fact]
        public void Top_SetsTopCount()
        {
            var builder = new QueryExpressionBuilder<TestEntity>()
                .Top(5);

            var query = builder.Build();
            Assert.Equal(5, query.TopCount);
        }
    }
}
