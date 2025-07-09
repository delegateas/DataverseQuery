# DataverseQuery

**Strongly-typed QueryExpression builder for Dataverse (.NET 8)**

## Overview

DataverseQuery provides a fluent, strongly-typed builder pattern for constructing `QueryExpression` objects for Microsoft Dataverse (Dynamics 365). It enables type-safe, readable, and maintainable query construction, including support for navigation property expansion and extension methods for easy retrieval.

- **Strongly-typed**: Use C# expressions for columns, filters, and relationships.
- **Fluent API**: Chainable methods for building complex queries.
- **Expandable**: Supports both 1:N and N:1 relationship expansion.
- **.NET 8 compatible**: Modern SDK and language features.

## Installation

Install via NuGet:

```
dotnet add package MSYS.Dataverse.QueryBuilder
```

Or via the NuGet Package Manager:

```
PM> Install-Package MSYS.Dataverse.QueryBuilder
```

## Usage

### Basic Query

```csharp
using DataverseQuery;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

// Example entity: Account
var builder = new QueryExpressionBuilder<Account>()
    .Select(a => a.Name, a => a.AccountNumber)
    .Where(a => a.StateCode == 0)
    .Top(10);

QueryExpression query = builder.Build();
```

### Expanding Relationships

```csharp
var builder = new QueryExpressionBuilder<Account>()
    .Select(a => a.Name)
    .Expand(a => a.PrimaryContactId, contact =>
        contact.Select(c => c.FullName)
               .Where(c => c.StateCode == 0)
    );
```

### Retrieve Entities

```csharp
IOrganizationService service = ...;
List<Account> accounts = service.RetrieveAll(builder);
```

## Features

- Strongly-typed column selection (`Select`)
- Strongly-typed filters (`Where`) — currently supports simple equality
- Relationship expansion (`Expand`) for both collections and references
- Top N results (`Top`)
- Extension method for `IOrganizationService` to retrieve entities

## Limitations

- The `Where` method currently only supports simple equality expressions (e.g., `e => e.Prop == value`).
- Relationship attribute mapping uses conventions and may require adjustment for complex scenarios.
- Advanced filter logic (AND/OR nesting, other operators) is not yet supported.

## Contributing

Contributions are welcome! Please open issues or pull requests on [GitHub](https://github.com/your-org/DataverseQuery).

## License

MIT License. See [LICENSE](LICENSE) for details.
