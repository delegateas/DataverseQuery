# Strongly-Typed Query Projections

This document demonstrates how to use the source generator to create strongly-typed result objects from your Dataverse queries.

## Overview

The `DataverseQuery` library includes a C# source generator that analyzes your query builder usage and **automatically generates both strongly-typed result classes AND mapper functions**. This provides:

- ✅ **Compile-time type safety** - No more runtime errors from typos
- ✅ **IntelliSense support** - Auto-complete for all selected properties
- ✅ **Zero boilerplate** - Mapper functions are auto-generated
- ✅ **Refactoring-friendly** - Rename properties with confidence
- ✅ **Self-documenting** - The result type shows exactly what data is available

## Quick Start - Auto-Generated Mappers

**The simplest way** - let the source generator create everything for you:

```csharp
using DataverseQuery.QueryBuilder;
using DataverseQuery.Generated;

// 1. Build your query with .Project()
var projection = new QueryExpressionBuilder<Account>()
    .Select(e => e.Name, e => e.AccountNumber, e => e.Revenue)
    .Project();

// 2. The source generator automatically creates:
//    - Query0Result class with Name, AccountNumber, Revenue properties
//    - ToQuery0Result() extension method with the complete mapper

// 3. Get the auto-generated mapper - NO MANUAL MAPPING CODE NEEDED!
var mapper = projection.ToQuery0Result();

// 4. Execute query
var query = projection.Build();
var results = service.RetrieveMultiple(query);

// 5. Map to strongly-typed results
var accounts = results.Entities.Select(mapper).ToList();

// 6. Use with full IntelliSense!
foreach (var account in accounts)
{
    Console.WriteLine($"{account.Name} - {account.AccountNumber}");
    Console.WriteLine($"Revenue: {account.Revenue:C}");
}
```

### With Linked Entities

```csharp
using DataverseQuery.QueryBuilder;
using DataverseQuery.Generated;

// Build query with linked entities
var projection = new QueryExpressionBuilder<Account>()
    .Select(e => e.Name, e => e.AccountNumber)
    .Expand(a => a.account_primary_contact,
        c => c.Select(x => x.FirstName, x => x.LastName, x => x.EmailAddress1))
    .Project();

// Auto-generated mapper includes nested linked entities!
var mapper = projection.ToQuery1Result();

var query = projection.Build();
var results = service.RetrieveMultiple(query);

foreach (var entity in results.Entities)
{
    var account = mapper(entity);
    Console.WriteLine($"Account: {account.Name}");

    // Nested properties with full IntelliSense!
    if (account.account_primary_contact != null)
    {
        var contact = account.account_primary_contact;
        Console.WriteLine($"  Contact: {contact.FirstName} {contact.LastName}");
        Console.WriteLine($"  Email: {contact.EmailAddress1}");
    }
}
```

## Basic Usage (Manual Mapper)

If you prefer to write the mapper manually, you can still use `.To()`:

### Simple Column Selection

```csharp
using DataverseQuery.QueryBuilder;
using DataverseQuery.Generated;

// Build your query with .Project() to enable source generation
var projection = new QueryExpressionBuilder<Account>()
    .Select(e => e.Name, e => e.AccountNumber, e => e.Revenue)
    .Project();

// Manual mapper (optional - auto-generated mapper is usually preferred)
var mapper = projection.To(proj => new
{
    Name = proj.Get(a => a.Name),
    AccountNumber = proj.Get(a => a.AccountNumber),
    Revenue = proj.Get(a => a.Revenue)
});

// Execute the query
var query = projection.Build();
var results = service.RetrieveMultiple(query);

// Map to strongly-typed results
var accounts = results.Entities.Select(mapper).ToList();

// Now you have full IntelliSense!
foreach (var account in accounts)
{
    Console.WriteLine($"{account.Name} - {account.AccountNumber}");
    Console.WriteLine($"Revenue: {account.Revenue:C}");
}
```

## Linked Entities (Related Records)

### Single Linked Entity

```csharp
var projection = new QueryExpressionBuilder<Account>()
    .Select(e => e.Name, e => e.AccountNumber)
    .Expand(a => a.account_primary_contact,
        c => c.Select(x => x.FirstName, x => x.LastName, x => x.EmailAddress1))
    .Project();

var mapper = projection.To(proj => new
{
    Name = proj.Get(a => a.Name),
    AccountNumber = proj.Get(a => a.AccountNumber),
    PrimaryContact = proj.GetLinked(
        a => a.account_primary_contact,
        contact => new
        {
            FirstName = contact.Get<Contact, string>(c => c.FirstName),
            LastName = contact.Get<Contact, string>(c => c.LastName),
            Email = contact.Get<Contact, string>(c => c.EmailAddress1)
        })
});

var query = projection.Build();
var results = service.RetrieveMultiple(query);

foreach (var entity in results.Entities)
{
    var account = mapper(entity);
    Console.WriteLine($"Account: {account.Name}");

    if (account.PrimaryContact != null)
    {
        Console.WriteLine($"  Contact: {account.PrimaryContact.FirstName} {account.PrimaryContact.LastName}");
        Console.WriteLine($"  Email: {account.PrimaryContact.Email}");
    }
}
```

### Nested Linked Entities

```csharp
// Query accounts with their parent account and the parent's primary contact
var projection = new QueryExpressionBuilder<Account>()
    .Select(e => e.Name, e => e.AccountNumber)
    .Expand(a => a.Referencingaccount_parent_account,
        parent => parent
            .Select(p => p.Name, p => p.AccountNumber)
            .Expand(p => p.account_primary_contact,
                contact => contact.Select(c => c.FirstName, c => c.LastName)))
    .Project();

var mapper = projection.To(proj => new
{
    Name = proj.Get(a => a.Name),
    AccountNumber = proj.Get(a => a.AccountNumber),
    ParentAccount = proj.GetLinked(
        a => a.Referencingaccount_parent_account,
        parent => new
        {
            Name = parent.Get<Account, string>(p => p.Name),
            AccountNumber = parent.Get<Account, string>(p => p.AccountNumber),
            PrimaryContact = parent.GetLinked<Account, Contact, object>(
                p => p.account_primary_contact,
                contact => new
                {
                    FirstName = contact.Get<Contact, string>(c => c.FirstName),
                    LastName = contact.Get<Contact, string>(c => c.LastName)
                })
        })
});

var query = projection.Build();
var results = service.RetrieveMultiple(query);

foreach (var entity in results.Entities)
{
    var account = mapper(entity);
    Console.WriteLine($"Account: {account.Name} ({account.AccountNumber})");

    if (account.ParentAccount != null)
    {
        Console.WriteLine($"  Parent: {account.ParentAccount.Name} ({account.ParentAccount.AccountNumber})");

        if (account.ParentAccount.PrimaryContact != null)
        {
            Console.WriteLine($"  Parent Contact: {account.ParentAccount.PrimaryContact.FirstName} {account.ParentAccount.PrimaryContact.LastName}");
        }
    }
}
```

### Multiple Linked Entities

```csharp
var projection = new QueryExpressionBuilder<Account>()
    .Select(e => e.Name)
    .Expand(a => a.account_primary_contact,
        c => c.Select(x => x.FirstName, x => x.LastName))
    .Expand(a => a.Referencingaccount_parent_account,
        p => p.Select(x => x.Name, x => x.AccountNumber))
    .Expand(a => a.CreatedBy,
        u => u.Select(x => x.FullName))
    .Project();

var mapper = projection.To(proj => new
{
    Name = proj.Get(a => a.Name),
    PrimaryContact = proj.GetLinked(
        a => a.account_primary_contact,
        contact => new { /* ... */ }),
    ParentAccount = proj.GetLinked(
        a => a.Referencingaccount_parent_account,
        parent => new { /* ... */ }),
    CreatedBy = proj.GetLinked(
        a => a.CreatedBy,
        user => new { /* ... */ })
});
```

## Using with Filters

You can combine projections with filters as normal:

```csharp
var projection = new QueryExpressionBuilder<Account>()
    .Select(e => e.Name, e => e.Revenue)
    .Where(e => e.StateCode, ConditionOperator.Equal, AccountState.Active)
    .Where(e => e.Revenue, ConditionOperator.GreaterThan, 1000000)
    .Expand(a => a.account_primary_contact,
        c => c.Select(x => x.FirstName, x => x.LastName)
              .Where(x => x.EmailAddress1, ConditionOperator.NotNull))
    .Top(100)
    .Project();

var mapper = projection.To(proj => new
{
    Name = proj.Get(a => a.Name),
    Revenue = proj.Get(a => a.Revenue),
    PrimaryContact = proj.GetLinked(
        a => a.account_primary_contact,
        contact => new
        {
            FirstName = contact.Get<Contact, string>(c => c.FirstName),
            LastName = contact.Get<Contact, string>(c => c.LastName)
        })
});
```

## Defining Result Classes

Instead of anonymous types, you can define your own result classes:

```csharp
public class AccountResult
{
    public string Name { get; set; }
    public string AccountNumber { get; set; }
    public decimal? Revenue { get; set; }
    public ContactResult PrimaryContact { get; set; }
}

public class ContactResult
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}

// Use them with the mapper
var projection = new QueryExpressionBuilder<Account>()
    .Select(e => e.Name, e => e.AccountNumber, e => e.Revenue)
    .Expand(a => a.account_primary_contact,
        c => c.Select(x => x.FirstName, x => x.LastName, x => x.EmailAddress1))
    .Project();

var mapper = projection.To(proj => new AccountResult
{
    Name = proj.Get(a => a.Name),
    AccountNumber = proj.Get(a => a.AccountNumber),
    Revenue = proj.Get(a => a.Revenue),
    PrimaryContact = proj.GetLinked(
        a => a.account_primary_contact,
        contact => new ContactResult
        {
            FirstName = contact.Get<Contact, string>(c => c.FirstName),
            LastName = contact.Get<Contact, string>(c => c.LastName),
            Email = contact.Get<Contact, string>(c => c.EmailAddress1)
        })
});
```

## Complete Example

```csharp
using DataverseQuery.QueryBuilder;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

public class AccountService
{
    private readonly IOrganizationService service;

    public AccountService(IOrganizationService service)
    {
        this.service = service;
    }

    public List<AccountWithContact> GetActiveAccountsWithContacts(decimal minRevenue)
    {
        // Build strongly-typed query
        var projection = new QueryExpressionBuilder<Account>()
            .Select(e => e.Name, e => e.AccountNumber, e => e.Revenue)
            .Where(e => e.StateCode, ConditionOperator.Equal, AccountState.Active)
            .Where(e => e.Revenue, ConditionOperator.GreaterThan, minRevenue)
            .Expand(a => a.account_primary_contact,
                c => c.Select(x => x.FirstName, x => x.LastName, x => x.EmailAddress1)
                      .Where(x => x.StateCode, ConditionOperator.Equal, ContactState.Active))
            .OrderBy(e => e.Revenue, OrderType.Descending)
            .Top(50)
            .Project();

        // Create strongly-typed mapper
        var mapper = projection.To(proj => new AccountWithContact
        {
            Name = proj.Get(a => a.Name),
            AccountNumber = proj.Get(a => a.AccountNumber),
            Revenue = proj.Get(a => a.Revenue),
            PrimaryContact = proj.GetLinked(
                a => a.account_primary_contact,
                contact => new ContactInfo
                {
                    FirstName = contact.Get<Contact, string>(c => c.FirstName),
                    LastName = contact.Get<Contact, string>(c => c.LastName),
                    Email = contact.Get<Contact, string>(c => c.EmailAddress1)
                })
        });

        // Execute query
        var query = projection.Build();
        var results = service.RetrieveMultiple(query);

        // Map to strongly-typed results
        return results.Entities.Select(mapper).ToList();
    }
}

public class AccountWithContact
{
    public string Name { get; set; }
    public string AccountNumber { get; set; }
    public decimal? Revenue { get; set; }
    public ContactInfo PrimaryContact { get; set; }
}

public class ContactInfo
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}
```

## How It Works

1. **Call `.Project()`** on your `QueryExpressionBuilder` to mark it for source generation
2. **The source generator analyzes** your `.Select()` and `.Expand()` calls at compile-time
3. **Result types are generated** in the `DataverseQuery.Generated` namespace with properties matching your selections
4. **Mapper extension methods are auto-generated** as `ToQuery{N}Result()` on `ProjectionBuilder<TEntity>`
5. **Call the auto-generated mapper** method to get a strongly-typed `Func<Entity, TResult>`
6. **Execute and map** your query results with full type safety

### What Gets Generated

For each query with `.Project()`, the source generator creates:

- **Result class** (`Query{N}Result`) with properties for each selected column
- **Nested result classes** for each linked entity (`Query{N}Result_{NavigationName}`)
- **Extension method** (`ToQuery{N}Result()`) with the complete mapping logic already written

All generated code appears in the `DataverseQuery.Generated` namespace with full IntelliSense support throughout your codebase.
