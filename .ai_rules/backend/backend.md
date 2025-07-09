---
description: Core rules for C# development and tooling
globs: *.cs,*.csproj,*.sln,Directory.Packages.props,global.json,dotnet-tools.json
alwaysApply: false
---
# Backend

Carefully follow these instructions for C# backend development, including code style, naming, exceptions, logging, and build/test/format workflow.

## Code Style

- Always use these C# features:
  - Top-level namespaces.
  - Primary constructors.
  - Array initializers.
  - Pattern matching with `is null` and `is not null` instead of `== null` and `!= null`.
- Records for immutable types.
- Mark all C# types as sealed.
- Use `var` when possible.
- Use simple collection types like `UserId[]` instead of `List<UserId>` whenever possible.
- Use clear names instead of making comments.
- Never use acronyms. E.g., use `SharedAccessSignature` instead of `Sas`.
- Do **not** prefix private fields with `_`. Use `camelCase` for private fields (e.g., `private readonly ITracingService tracingService;`). Follow the existing codebase conventions for field naming.
- Avoid using exceptions for control flow:
  - When exceptions are thrown, always use meaningful exceptions following .NET conventions.
  - Exception messages should include a period.
- Log only meaningful events at appropriate severity levels.
  - Logging messages should not include a period.
  - Use structured logging.
- Never introduce new NuGet dependencies.
- Don't do defensive coding (e.g., do not add exception handling to handle situations we don't know will happen).
- Do not add null checks for plugin context Target entities unless there is a documented scenario where Target may be missing. The framework guarantees the presence of the Target for the registered entity and operation.
- Avoid try-catch unless we cannot fix the reason. We have global exception handling to handle unknown exceptions.
- Don't add comments unless the code is truly not expressing the intent.
- Never add XML comments.
- Business logic is grouped by area. If something does not fit under an existing area you should ask what area it should be under. After asking, if that area does not exist, you create a folder with the area.
- Prefer constructor dependency injection for dependency management. Only deviate from this when applying a strategy + factory pattern. 

## Implementation

IMPORTANT: Always follow these steps very carefully when implementing changes:

1. Always start new changes by writing new test cases (or change existing tests).
2. Build and test your changes:
   - Always run `dotnet build --configuration Release` to build the backend in release mode.
   - Run `dotnet test` to run all tests.
3. Format the code by running `dotnet format`. This will format the code according to the .editorconfig file.