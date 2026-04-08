---
description: 'Guidance for working with modern .NET (net10.0) SDK-style projects. Includes project structure, C# language features, NuGet management, and best practices.'
applyTo: '**/*.csproj, **/*.cs'
---

# Modern .NET Development (net10.0)

## Build and Compilation Requirements
- Use `dotnet build` to build projects and `dotnet build -c Release -p:Platform=x64` for release builds.
- Use `dotnet restore` to restore NuGet packages before building.

## Project File Management

### SDK-Style Project Structure
This project uses modern SDK-style `.csproj` files:

- **Implicit File Inclusion**: Source files are automatically included — do **not** add `<Compile>` elements manually.
- **Target Framework**: Uses `<TargetFramework>net10.0</TargetFramework>`.
- **Nullable Reference Types**: Enabled via `<Nullable>enable</Nullable>` — language rules for nullability are in `csharp.instructions.md`.
- **Platform**: Projects target `x64`; always pass `-p:Platform=x64` when building from the CLI.

## NuGet Package Management
- Use `dotnet add package <PackageName>` to add NuGet packages.
- Ensure new packages are compatible with `net10.0`.
- Keep `<PackageReference>` entries in the `.csproj`; do not use `packages.config`.

## C# Language Version
- This project targets `net10.0` with **C# 14**. All modern language features are available and encouraged — see `csharp.instructions.md` for language style rules and patterns.

## Environment Considerations (Windows)
- Use Windows-style paths with backslashes (e.g., `C:\path\to\file.cs`).
- Use PowerShell-compatible commands when suggesting terminal operations.

## Best Practices

### Async/Await Patterns
- **Avoid sync-over-async**: Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` — always `await`.
- **ConfigureAwait**: Not required in application code; use `ConfigureAwait(false)` only in library code that may run outside a UI/ASP.NET context.
- **CancellationToken**: Accept and propagate `CancellationToken` in all async public APIs.

### DateTime Handling
- **Prefer `DateTimeOffset`** over `DateTime` for absolute timestamps to avoid timezone ambiguity.
- **Use `TimeProvider`** (net8+) for testable time abstractions instead of `DateTime.Now`.
- **Culture-aware formatting**: Use `CultureInfo.InvariantCulture` for serialization/parsing.

### String Operations
- **Prefer interpolated strings** or `string.Create` over `StringBuilder` for simple concatenation.
- **Use `StringBuilder`** only for many concatenations in a loop.
- **Always specify `StringComparison`** for string comparisons:
  ```csharp
  string.Equals(other, StringComparison.OrdinalIgnoreCase)
  ```

### Memory Management
- **Dispose pattern**: Implement `IDisposable` and/or `IAsyncDisposable` for unmanaged resources.
- **`using` declarations**: Use `using var` for concise, scope-bound disposal.
- **Span\<T\> and Memory\<T\>**: Prefer stack-allocated `Span<T>` for short-lived buffers to reduce heap allocations.
- **`ArrayPool<T>`**: Use for temporary large arrays instead of allocating new arrays.

### Exception Handling
- **Specific exceptions**: Catch specific exception types, not the base `Exception`.
- **Don't swallow exceptions**: Always log or re-throw; use `ExceptionDispatchInfo` when rethrowing across async boundaries.

### Performance Considerations
- **Avoid boxing**: Use generics and `Span<T>` to avoid value-type boxing.
- **`Lazy<T>`**: Use for expensive deferred initialization.
- **Avoid reflection in hot paths**: Cache `MethodInfo`/`PropertyInfo` objects or prefer source generators.
