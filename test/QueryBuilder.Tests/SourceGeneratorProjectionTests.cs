using DataverseQuery.QueryBuilder;
using DataverseQuery.Generated;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseQuery.Tests
{
    /// <summary>
    /// Tests for source-generated projection types.
    /// The source generator will analyze .Project() calls and generate result types.
    /// </summary>
    public class SourceGeneratorProjectionTests
    {
        [Fact]
        public void Project_SimpleColumns_GeneratesStronglyTypedResult()
        {
            // Arrange - Build query with .Project() to trigger source generator
            var projection = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name, e => e.AccountNumber)
                .Project();

            // The source generator will create Query0Result class with Name and AccountNumber properties
            var mapper = projection.To(proj => new
            {
                Name = proj.Get(a => a.Name),
                AccountNumber = proj.Get(a => a.AccountNumber)
            });

            var query = projection.Build();

            // Create test entity
            var entity = new Entity("account")
            {
                ["name"] = "Test Account",
                ["accountnumber"] = "ACC-001"
            };

            // Act
            var result = mapper(entity);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Account", result.Name);
            Assert.Equal("ACC-001", result.AccountNumber);
        }

        [Fact]
        public void Project_WithLinkedEntity_GeneratesNestedStronglyTypedResult()
        {
            // Arrange
            var projection = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name, e => e.AccountNumber)
                .Expand(a => a.account_primary_contact,
                    c => c.Select(x => x.FirstName, x => x.LastName))
                .Project();

            // Source generator creates Query1Result with nested Query1Result_account_primary_contact
            var mapper = projection.To(proj => new
            {
                Name = proj.Get(a => a.Name),
                AccountNumber = proj.Get(a => a.AccountNumber),
                PrimaryContact = proj.GetLinked(
                    a => a.account_primary_contact,
                    contact => new
                    {
                        FirstName = contact.Get<SharedContext.Contact, string>(c => c.FirstName),
                        LastName = contact.Get<SharedContext.Contact, string>(c => c.LastName)
                    })
            });

            var query = projection.Build();

            // Create test entity with linked data
            var entity = new Entity("account")
            {
                ["name"] = "Test Account",
                ["accountnumber"] = "ACC-001"
            };

            var linkAlias = query.LinkEntities[0].EntityAlias;
            entity[$"{linkAlias}.firstname"] = new AliasedValue("contact", "firstname", "John");
            entity[$"{linkAlias}.lastname"] = new AliasedValue("contact", "lastname", "Doe");

            // Act
            var result = mapper(entity);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Account", result.Name);
            Assert.Equal("ACC-001", result.AccountNumber);
            Assert.NotNull(result.PrimaryContact);
            Assert.Equal("John", result.PrimaryContact.FirstName);
            Assert.Equal("Doe", result.PrimaryContact.LastName);
        }

        [Fact]
        public void Project_WithNestedLinkedEntities_GeneratesDeepNestedTypes()
        {
            // Arrange
            var projection = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name)
                .Expand(a => a.Referencingaccount_parent_account,
                    parent => parent
                        .Select(p => p.Name, p => p.AccountNumber)
                        .Expand(p => p.account_primary_contact,
                            contact => contact.Select(c => c.FirstName, c => c.LastName)))
                .Project();

            // Source generator creates nested result types
            var mapper = projection.To(proj => new
            {
                Name = proj.Get(a => a.Name),
                ParentAccount = proj.GetLinked(
                    a => a.Referencingaccount_parent_account,
                    parent => new
                    {
                        Name = parent.Get<SharedContext.Account, string>(p => p.Name),
                        AccountNumber = parent.Get<SharedContext.Account, string>(p => p.AccountNumber),
                        PrimaryContact = parent.GetLinked<SharedContext.Account, SharedContext.Contact, object>(
                            p => p.account_primary_contact,
                            contact => new
                            {
                                FirstName = contact.Get<SharedContext.Contact, string>(c => c.FirstName),
                                LastName = contact.Get<SharedContext.Contact, string>(c => c.LastName)
                            })
                    })
            });

            var query = projection.Build();

            // Create test entity
            var entity = new Entity("account")
            {
                ["name"] = "Child Account"
            };

            var parentAlias = query.LinkEntities[0].EntityAlias;
            var contactAlias = query.LinkEntities[0].LinkEntities[0].EntityAlias;

            entity[$"{parentAlias}.name"] = new AliasedValue("account", "name", "Parent Account");
            entity[$"{parentAlias}.accountnumber"] = new AliasedValue("account", "accountnumber", "PARENT-001");
            entity[$"{contactAlias}.firstname"] = new AliasedValue("contact", "firstname", "Jane");
            entity[$"{contactAlias}.lastname"] = new AliasedValue("contact", "lastname", "Smith");

            // Act
            var result = mapper(entity);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Child Account", result.Name);
            Assert.NotNull(result.ParentAccount);
            Assert.Equal("Parent Account", result.ParentAccount.Name);
            Assert.Equal("PARENT-001", result.ParentAccount.AccountNumber);
            Assert.NotNull(result.ParentAccount.PrimaryContact);
            Assert.Equal("Jane", result.ParentAccount.PrimaryContact.FirstName);
            Assert.Equal("Smith", result.ParentAccount.PrimaryContact.LastName);
        }

        [Fact]
        public void Project_WithMultipleLinkedEntities_GeneratesAllNestedTypes()
        {
            // Arrange
            var projection = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name)
                .Expand(a => a.account_primary_contact, c => c.Select(x => x.FirstName))
                .Expand(a => a.Referencingaccount_parent_account, p => p.Select(x => x.AccountNumber))
                .Project();

            var mapper = projection.To(proj => new
            {
                Name = proj.Get(a => a.Name),
                PrimaryContact = proj.GetLinked(
                    a => a.account_primary_contact,
                    contact => new
                    {
                        FirstName = contact.Get<SharedContext.Contact, string>(c => c.FirstName)
                    }),
                ParentAccount = proj.GetLinked(
                    a => a.Referencingaccount_parent_account,
                    parent => new
                    {
                        AccountNumber = parent.Get<SharedContext.Account, string>(p => p.AccountNumber)
                    })
            });

            var query = projection.Build();

            // Create test entity
            var entity = new Entity("account")
            {
                ["name"] = "Test Account"
            };

            var contactAlias = query.LinkEntities[0].EntityAlias;
            var parentAlias = query.LinkEntities[1].EntityAlias;

            entity[$"{contactAlias}.firstname"] = new AliasedValue("contact", "firstname", "John");
            entity[$"{parentAlias}.accountnumber"] = new AliasedValue("account", "accountnumber", "PARENT-001");

            // Act
            var result = mapper(entity);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Account", result.Name);
            Assert.NotNull(result.PrimaryContact);
            Assert.Equal("John", result.PrimaryContact.FirstName);
            Assert.NotNull(result.ParentAccount);
            Assert.Equal("PARENT-001", result.ParentAccount.AccountNumber);
        }

        [Fact]
        public void Project_OnlyMainEntityColumns_WorksWithoutLinkedEntities()
        {
            // Arrange
            var projection = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name, e => e.AccountNumber, e => e.StateCode)
                .Where(e => e.StateCode, ConditionOperator.Equal, SharedContext.AccountState.Aktiv)
                .Project();

            var mapper = projection.To(proj => new
            {
                Name = proj.Get(a => a.Name),
                AccountNumber = proj.Get(a => a.AccountNumber),
                StateCode = proj.Get(a => a.StateCode)
            });

            var entity = new Entity("account")
            {
                ["name"] = "Active Account",
                ["accountnumber"] = "ACC-100",
                ["statecode"] = new OptionSetValue(0)
            };

            // Act
            var result = mapper(entity);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Active Account", result.Name);
            Assert.Equal("ACC-100", result.AccountNumber);
            Assert.NotNull(result.StateCode);
        }
    }
}
