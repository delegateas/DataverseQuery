using DataverseQuery.QueryBuilder;
using DataverseQuery.QueryBuilder.Extensions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Dynamic;

namespace DataverseQuery.Tests
{
    public class ResultMapperTests
    {
        [Fact]
        public void GetResultMapper_WithSimpleColumns_ReturnsMapperWithSelectedColumns()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name, e => e.AccountNumber);

            // Act
            var mapper = builder.GetResultMapper();
            var mapFunc = mapper.CreateMapper();

            // Create a test entity
            var entity = new Entity("account")
            {
                ["name"] = "Test Account",
                ["accountnumber"] = "ACC-001"
            };

            var result = mapFunc(entity);

            // Assert
            Assert.NotNull(result);
            IDictionary<string, object?> resultDict = result;
            Assert.Equal("Test Account", resultDict["name"]);
            Assert.Equal("ACC-001", resultDict["accountnumber"]);
        }

        [Fact]
        public void GetResultMapper_WithMissingColumn_ReturnsNullForMissingValue()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name, e => e.AccountNumber);

            // Act
            var mapper = builder.GetResultMapper();
            var mapFunc = mapper.CreateMapper();

            // Create a test entity with only one column
            var entity = new Entity("account")
            {
                ["name"] = "Test Account"
            };

            var result = mapFunc(entity);

            // Assert
            Assert.NotNull(result);
            IDictionary<string, object?> resultDict = result;
            Assert.Equal("Test Account", resultDict["name"]);
            Assert.Null(resultDict["accountnumber"]);
        }

        [Fact]
        public void GetResultMapper_WithLinkedEntity_ReturnsNestedObject()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name)
                .Expand(a => a.account_primary_contact, c => c.Select(x => x.FirstName, x => x.LastName));

            // Act
            var query = builder.Build();
            var mapper = builder.GetResultMapper();
            var mapFunc = mapper.CreateMapper();

            // Create a test entity with aliased values
            var entity = new Entity("account")
            {
                ["name"] = "Test Account"
            };

            // Get the alias from the link entity
            var linkAlias = query.LinkEntities[0].EntityAlias;
            entity[$"{linkAlias}.firstname"] = new AliasedValue("contact", "firstname", "John");
            entity[$"{linkAlias}.lastname"] = new AliasedValue("contact", "lastname", "Doe");

            var result = mapFunc(entity);

            // Assert
            Assert.NotNull(result);
            IDictionary<string, object?> resultDict = result;
            Assert.Equal("Test Account", resultDict["name"]);
            Assert.NotNull(resultDict["account_primary_contact"]);

            var linkedEntity = (IDictionary<string, object?>)resultDict["account_primary_contact"];
            Assert.Equal("John", linkedEntity["firstname"]);
            Assert.Equal("Doe", linkedEntity["lastname"]);
        }

        [Fact]
        public void GetResultMapper_WithMissingLinkedEntity_ReturnsNull()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name)
                .Expand(a => a.account_primary_contact, c => c.Select(x => x.FirstName, x => x.LastName));

            // Act
            var mapper = builder.GetResultMapper();
            var mapFunc = mapper.CreateMapper();

            // Create a test entity without linked entity values
            var entity = new Entity("account")
            {
                ["name"] = "Test Account"
            };

            var result = mapFunc(entity);

            // Assert
            Assert.NotNull(result);
            IDictionary<string, object?> resultDict = result;
            Assert.Equal("Test Account", resultDict["name"]);
            Assert.Null(resultDict["account_primary_contact"]);
        }

        [Fact]
        public void GetResultMapper_WithNestedLinkedEntities_ReturnsDeepNestedObject()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name)
                .Expand(a => a.Referencingaccount_parent_account,
                    parent => parent
                        .Select(p => p.Name)
                        .Expand(p => p.account_primary_contact,
                            contact => contact.Select(c => c.FirstName, c => c.LastName)));

            // Act
            var query = builder.Build();
            var mapper = builder.GetResultMapper();
            var mapFunc = mapper.CreateMapper();

            // Create a test entity with nested aliased values
            var entity = new Entity("account")
            {
                ["name"] = "Child Account"
            };

            // Get the aliases
            var parentLinkAlias = query.LinkEntities[0].EntityAlias;
            var contactLinkAlias = query.LinkEntities[0].LinkEntities[0].EntityAlias;

            entity[$"{parentLinkAlias}.name"] = new AliasedValue("account", "name", "Parent Account");
            entity[$"{contactLinkAlias}.firstname"] = new AliasedValue("contact", "firstname", "Jane");
            entity[$"{contactLinkAlias}.lastname"] = new AliasedValue("contact", "lastname", "Smith");

            var result = mapFunc(entity);

            // Assert
            Assert.NotNull(result);
            IDictionary<string, object?> resultDict = result;
            Assert.Equal("Child Account", resultDict["name"]);
            Assert.NotNull(resultDict["account_parent_account"]);

            var parentEntity = (IDictionary<string, object?>)resultDict["account_parent_account"];
            Assert.Equal("Parent Account", parentEntity["name"]);
            Assert.NotNull(parentEntity["account_primary_contact"]);

            var contactEntity = (IDictionary<string, object?>)parentEntity["account_primary_contact"];
            Assert.Equal("Jane", contactEntity["firstname"]);
            Assert.Equal("Smith", contactEntity["lastname"]);
        }

        [Fact]
        public void GetResultMapper_WithMultipleLinkedEntities_ReturnsBothLinkedObjects()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name)
                .Expand(a => a.account_primary_contact, c => c.Select(x => x.FirstName))
                .Expand(a => a.Referencingaccount_parent_account, p => p.Select(x => x.AccountNumber));

            // Act
            var query = builder.Build();
            var mapper = builder.GetResultMapper();
            var mapFunc = mapper.CreateMapper();

            // Create a test entity with multiple linked entities
            var entity = new Entity("account")
            {
                ["name"] = "Test Account"
            };

            var contactAlias = query.LinkEntities[0].EntityAlias;
            var parentAlias = query.LinkEntities[1].EntityAlias;

            entity[$"{contactAlias}.firstname"] = new AliasedValue("contact", "firstname", "John");
            entity[$"{parentAlias}.accountnumber"] = new AliasedValue("account", "accountnumber", "PARENT-001");

            var result = mapFunc(entity);

            // Assert
            Assert.NotNull(result);
            IDictionary<string, object?> resultDict = result;
            Assert.Equal("Test Account", resultDict["name"]);

            var contactEntity = (IDictionary<string, object?>)resultDict["account_primary_contact"];
            Assert.NotNull(contactEntity);
            Assert.Equal("John", contactEntity["firstname"]);

            var parentEntity = (IDictionary<string, object?>)resultDict["account_parent_account"];
            Assert.NotNull(parentEntity);
            Assert.Equal("PARENT-001", parentEntity["accountnumber"]);
        }

        [Fact]
        public void BuildLinkEntity_AssignsUniqueAliases()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Expand(a => a.account_primary_contact, c => c.Select(x => x.FirstName))
                .Expand(a => a.Referencingaccount_parent_account, p => p.Select(x => x.AccountNumber));

            // Act
            var query = builder.Build();

            // Assert
            Assert.Equal(2, query.LinkEntities.Count);
            Assert.NotNull(query.LinkEntities[0].EntityAlias);
            Assert.NotNull(query.LinkEntities[1].EntityAlias);
            Assert.NotEqual(query.LinkEntities[0].EntityAlias, query.LinkEntities[1].EntityAlias);
        }

        [Fact]
        public void EntityExtensions_GetAliasedValue_ReturnsCorrectValue()
        {
            // Arrange
            var entity = new Entity("account");
            entity["link0.firstname"] = new AliasedValue("contact", "firstname", "John");

            // Act
            var result = entity.GetAliasedValue<string>("link0", "firstname");

            // Assert
            Assert.Equal("John", result);
        }

        [Fact]
        public void EntityExtensions_GetAliasedValue_WithMissingValue_ReturnsDefault()
        {
            // Arrange
            var entity = new Entity("account");

            // Act
            var result = entity.GetAliasedValue<string>("link0", "firstname");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void EntityExtensions_GetAliasedValue_WithWrongType_ReturnsDefault()
        {
            // Arrange
            var entity = new Entity("account");
            entity["link0.count"] = new AliasedValue("contact", "count", "not a number");

            // Act
            var result = entity.GetAliasedValue<int>("link0", "count");

            // Assert
            Assert.Equal(0, result);
        }
    }
}
