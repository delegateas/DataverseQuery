using DataverseQuery.QueryBuilder;
using DataverseQuery.QueryBuilder.Extensions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseQuery.Tests
{
    // Test DTOs
    public class AccountDto
    {
        public string? Name { get; set; }
        public string? AccountNumber { get; set; }
        public ContactDto? PrimaryContact { get; set; }
        public AccountDto? ParentAccount { get; set; }
    }

    public class ContactDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    public class NestedAccountDto
    {
        public string? Name { get; set; }
        public ParentAccountDto? ParentAccount { get; set; }
    }

    public class ParentAccountDto
    {
        public string? Name { get; set; }
        public string? AccountNumber { get; set; }
        public ContactDto? PrimaryContact { get; set; }
    }

    public class StronglyTypedResultMapperTests
    {
        [Fact]
        public void GetResultMapper_WithSimpleColumns_ReturnsStronglyTypedResult()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name, e => e.AccountNumber);

            var mapper = builder.GetResultMapper(proj => new AccountDto
            {
                Name = proj.Get(a => a.Name),
                AccountNumber = proj.Get(a => a.AccountNumber)
            });

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
            Assert.Null(result.PrimaryContact);
        }

        [Fact]
        public void GetResultMapper_WithMissingColumn_ReturnsNull()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name, e => e.AccountNumber);

            var mapper = builder.GetResultMapper(proj => new AccountDto
            {
                Name = proj.Get(a => a.Name),
                AccountNumber = proj.Get(a => a.AccountNumber)
            });

            var entity = new Entity("account")
            {
                ["name"] = "Test Account"
                // accountnumber is missing
            };

            // Act
            var result = mapper(entity);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Account", result.Name);
            Assert.Null(result.AccountNumber);
        }

        [Fact]
        public void GetResultMapper_WithLinkedEntity_ReturnsStronglyTypedNestedObject()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name)
                .Expand(a => a.account_primary_contact,
                    c => c.Select(x => x.FirstName, x => x.LastName));

            var query = builder.Build();
            var mapper = builder.GetResultMapper(proj => new AccountDto
            {
                Name = proj.Get(a => a.Name),
                PrimaryContact = proj.GetLinked(
                    a => a.account_primary_contact,
                    contact => new ContactDto
                    {
                        FirstName = contact.Get<SharedContext.Contact, string>(c => c.FirstName),
                        LastName = contact.Get<SharedContext.Contact, string>(c => c.LastName)
                    })
            });

            var entity = new Entity("account")
            {
                ["name"] = "Test Account"
            };

            var linkAlias = query.LinkEntities[0].EntityAlias;
            entity[$"{linkAlias}.firstname"] = new AliasedValue("contact", "firstname", "John");
            entity[$"{linkAlias}.lastname"] = new AliasedValue("contact", "lastname", "Doe");

            // Act
            var result = mapper(entity);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Account", result.Name);
            Assert.NotNull(result.PrimaryContact);
            Assert.Equal("John", result.PrimaryContact.FirstName);
            Assert.Equal("Doe", result.PrimaryContact.LastName);
        }

        [Fact]
        public void GetResultMapper_WithMissingLinkedEntity_ReturnsNullForLinkedObject()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name)
                .Expand(a => a.account_primary_contact,
                    c => c.Select(x => x.FirstName, x => x.LastName));

            var mapper = builder.GetResultMapper(proj => new AccountDto
            {
                Name = proj.Get(a => a.Name),
                PrimaryContact = proj.GetLinked(
                    a => a.account_primary_contact,
                    contact => new ContactDto
                    {
                        FirstName = contact.Get<SharedContext.Contact, string>(c => c.FirstName),
                        LastName = contact.Get<SharedContext.Contact, string>(c => c.LastName)
                    })
            });

            var entity = new Entity("account")
            {
                ["name"] = "Test Account"
                // No linked entity data
            };

            // Act
            var result = mapper(entity);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Account", result.Name);
            Assert.Null(result.PrimaryContact);
        }

        [Fact]
        public void GetResultMapper_WithNestedLinkedEntities_ReturnsDeepNestedStronglyTypedObject()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name)
                .Expand(a => a.Referencingaccount_parent_account,
                    parent => parent
                        .Select(p => p.Name, p => p.AccountNumber)
                        .Expand(p => p.account_primary_contact,
                            contact => contact.Select(c => c.FirstName, c => c.LastName)));

            var query = builder.Build();
            var mapper = builder.GetResultMapper(proj => new NestedAccountDto
            {
                Name = proj.Get(a => a.Name),
                ParentAccount = proj.GetLinked(
                    a => a.Referencingaccount_parent_account,
                    parent => new ParentAccountDto
                    {
                        Name = parent.Get<SharedContext.Account, string>(p => p.Name),
                        AccountNumber = parent.Get<SharedContext.Account, string>(p => p.AccountNumber),
                        PrimaryContact = parent.GetLinked<SharedContext.Account, SharedContext.Contact, ContactDto>(
                            p => p.account_primary_contact,
                            contact => new ContactDto
                            {
                                FirstName = contact.Get<SharedContext.Contact, string>(c => c.FirstName),
                                LastName = contact.Get<SharedContext.Contact, string>(c => c.LastName)
                            })
                    })
            });

            var entity = new Entity("account")
            {
                ["name"] = "Child Account"
            };

            var parentLinkAlias = query.LinkEntities[0].EntityAlias;
            var contactLinkAlias = query.LinkEntities[0].LinkEntities[0].EntityAlias;

            entity[$"{parentLinkAlias}.name"] = new AliasedValue("account", "name", "Parent Account");
            entity[$"{parentLinkAlias}.accountnumber"] = new AliasedValue("account", "accountnumber", "PARENT-001");
            entity[$"{contactLinkAlias}.firstname"] = new AliasedValue("contact", "firstname", "Jane");
            entity[$"{contactLinkAlias}.lastname"] = new AliasedValue("contact", "lastname", "Smith");

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
        public void GetResultMapper_WithMultipleLinkedEntities_ReturnsBothStronglyTypedObjects()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name)
                .Expand(a => a.account_primary_contact, c => c.Select(x => x.FirstName))
                .Expand(a => a.Referencingaccount_parent_account, p => p.Select(x => x.AccountNumber));

            var query = builder.Build();
            var mapper = builder.GetResultMapper(proj => new AccountDto
            {
                Name = proj.Get(a => a.Name),
                PrimaryContact = proj.GetLinked(
                    a => a.account_primary_contact,
                    contact => new ContactDto
                    {
                        FirstName = contact.Get<SharedContext.Contact, string>(c => c.FirstName)
                    }),
                ParentAccount = proj.GetLinked(
                    a => a.Referencingaccount_parent_account,
                    parent => new AccountDto
                    {
                        AccountNumber = parent.Get<SharedContext.Account, string>(p => p.AccountNumber)
                    })
            });

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
        public void GetResultMapper_WithCollectionNavigation_ReturnsStronglyTypedObject()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name)
                .Expand(a => a.contact_customer_accounts,
                    c => c.Select(x => x.FirstName, x => x.LastName));

            var query = builder.Build();
            var mapper = builder.GetResultMapper(proj => new AccountDto
            {
                Name = proj.Get(a => a.Name),
                PrimaryContact = proj.GetLinked(
                    a => a.contact_customer_accounts,
                    contact => new ContactDto
                    {
                        FirstName = contact.Get<SharedContext.Contact, string>(c => c.FirstName),
                        LastName = contact.Get<SharedContext.Contact, string>(c => c.LastName)
                    })
            });

            var entity = new Entity("account")
            {
                ["name"] = "Test Account"
            };

            var linkAlias = query.LinkEntities[0].EntityAlias;
            entity[$"{linkAlias}.firstname"] = new AliasedValue("contact", "firstname", "John");
            entity[$"{linkAlias}.lastname"] = new AliasedValue("contact", "lastname", "Doe");

            // Act
            var result = mapper(entity);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Account", result.Name);
            Assert.NotNull(result.PrimaryContact);
            Assert.Equal("John", result.PrimaryContact.FirstName);
            Assert.Equal("Doe", result.PrimaryContact.LastName);
        }

        [Fact]
        public void GetAliasMap_ReturnsCorrectAliasMapping()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Expand(a => a.account_primary_contact, c => c.Select(x => x.FirstName))
                .Expand(a => a.Referencingaccount_parent_account, p => p.Select(x => x.AccountNumber));

            // Act
            var aliasMap = builder.GetAliasMap();

            // Assert
            Assert.NotNull(aliasMap);
            Assert.Equal(2, aliasMap.Count);
            Assert.True(aliasMap.ContainsKey("account_primary_contact"));
            Assert.True(aliasMap.ContainsKey("account_parent_account"));
            Assert.NotNull(aliasMap["account_primary_contact"]);
            Assert.NotNull(aliasMap["account_parent_account"]);
        }

        [Fact]
        public void EntityExtensions_HasAliasedValues_ReturnsTrue_WhenValuesExist()
        {
            // Arrange
            var entity = new Entity("account");
            entity["link0.firstname"] = new AliasedValue("contact", "firstname", "John");

            // Act
            var hasValues = entity.HasAliasedValues("link0");

            // Assert
            Assert.True(hasValues);
        }

        [Fact]
        public void EntityExtensions_HasAliasedValues_ReturnsFalse_WhenNoValuesExist()
        {
            // Arrange
            var entity = new Entity("account");
            entity["link0.firstname"] = new AliasedValue("contact", "firstname", "John");

            // Act
            var hasValues = entity.HasAliasedValues("link1");

            // Assert
            Assert.False(hasValues);
        }

        [Fact]
        public void GetResultMapper_MultipleCalls_ReturnsConsistentAliases()
        {
            // Arrange
            var builder = new QueryExpressionBuilder<SharedContext.Account>()
                .Select(e => e.Name)
                .Expand(a => a.account_primary_contact, c => c.Select(x => x.FirstName));

            // Act
            var query1 = builder.Build();
            var query2 = builder.Build();

            // Assert
            Assert.Equal(query1.LinkEntities[0].EntityAlias, query2.LinkEntities[0].EntityAlias);
        }
    }
}
