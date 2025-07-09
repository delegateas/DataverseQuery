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
dotnet add package DataverseQuery
```

Or via the NuGet Package Manager:

```
PM> Install-Package DataverseQuery
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

## Features

- Strongly-typed column selection (`Select`)
- Strongly-typed filters (`Where`) — currently supports simple equality
- Relationship expansion (`Expand`) for both collections and references
- Top N results (`Top`)

## Limitations

- The `Where` method currently only supports simple equality expressions (e.g., `e => e.Prop == value`).
- Advanced filter logic (AND/OR nesting, other operators) is not yet supported.

## Development

### CI/CD Pipelines

This repository includes automated CI/CD pipelines:

- **PR Pipeline**: Runs on pull requests to validate code formatting, build, and tests
- **Release Pipeline**: Automatically publishes NuGet packages when version tags are pushed

To create a release:
1. Tag your commit with a version (e.g., `git tag v1.0.0`)
2. Push the tag (`git push origin v1.0.0`)
3. The release pipeline will automatically build and publish the NuGet package

**Note**: Publishing to NuGet requires the `NUGET_API_KEY` secret to be configured in the repository settings.

### Contributing

Before submitting a pull request:
1. Run `dotnet format` to ensure code formatting compliance
2. Run `dotnet build --configuration Release` to verify the build passes with zero warnings
3. Run `dotnet test --configuration Release` to ensure all tests pass

The PR pipeline enforces these requirements and treats all warnings as errors.

## License

MIT License. See [LICENSE](LICENSE) for details.
