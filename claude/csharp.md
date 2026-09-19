# C# and .NET

## Projects

- When contributing to an existing codebase, its conventions take precedence over everything in this file.
- Target the latest .NET, with `Nullable` and `ImplicitUsings` enabled and `<AnalysisLevel>10.0-all</AnalysisLevel>`.
  Builds must have no warnings.
- Name solutions, projects and folders `Invicta.<Area>` (`Invicta.Time`, `Invicta.Time.Tests`) and set
  `RootNamespace` to `Invicta`. Namespaces mirror the matching `System` namespace: a `TimeProvider` subclass goes
  in `Invicta`, threading types in `Invicta.Threading`. Folders match namespaces.
- Follow the prevailing folder structure of high-quality C# codebases: currently `src/`, `tests/`, `benchmarks/`
  and `samples/`, with an `.slnx` solution.
- No top-level statements: executables declare `internal static class Program` with a `private static Main`.
- Check style with `dotnet format --verify-no-changes --severity info`; a normal build doesn't report IDE
  suggestions such as the file header or naming.

## Code

- `.editorconfig` is authoritative for formatting and naming, including `_camelCase` private fields, `s_` static
  fields and explicit types instead of `var`.
- A name says what the thing is for. Prefer a longer name to a shorter one that needs a comment to explain it, and
  shorten any name that can lose words without losing its meaning.
- Abbreviate only in private field names, and only where the average developer would know the abbreviation in the
  context of the project. Elsewhere abbreviate only where the base libraries already do.
- Every file starts with `// © <year> Andrew Pollard. All rights reserved.` and `// Licensed under the MIT License.`
- Access modifiers show intent, even inside internal types: `public` for what would be public API if the type were
  public (including interface implementations and P/Invoke declarations), `internal` for assembly plumbing, and
  `private` for everything else.
- Use private fields rather than private properties.
- Prefer `is null`, `nameof`, pattern matching, switch expressions and throw helpers such as
  `ArgumentNullException.ThrowIfNull`. Trust nullable annotations rather than adding redundant null checks.
- Call numeric operations on the type rather than the `Math` class: `double.Sqrt`, `double.Pi`,
  `double.Ieee754Remainder`, `int.Max`, `long.DivRem`. Pick the type the arguments already have, so that a call which
  took the `int` or `float` overload does not quietly widen to `double`.
- Methods have block bodies, never `=>` expression bodies; properties may use `=>`.
- When implementing a framework abstraction, match the behaviour of its built-in implementation, and don't handle
  edge cases that it doesn't without a stated reason.
- Asynchronous methods end in `Async`, except test and benchmark methods, whose names the runners display; public
  ones take a `CancellationToken` as their last parameter and pass it on.
  Avoid `async void` outside event handlers, never block on async code with `.Result` or `.Wait()`, and use
  `ConfigureAwait(false)` in library code.
- Suppress an analyzer finding only when it can't reasonably be fixed, with
  `[SuppressMessage("Category", "ID:Title", Justification = "…")]` on the narrowest member or type, or
  `[assembly: SuppressMessage(…)]` in `GlobalSuppressions.cs`. Never use `<NoWarn>` or `#pragma warning disable`.

## Member order

- Order a type's members as fields, constructors, properties, methods, then nested types. Don't group them by
  accessibility, or by static and instance.
- Order fields by purpose, with a lock directly beneath the fields it guards.
- Order properties as a caller uses them, such as a support check before the instance it guards.
- Put a method directly after the member that calls it, so the file reads from the top down. A method with several
  callers goes after the last group of methods that calls it, and groups of methods follow the object's lifetime,
  such as construction before the operations that come after it.

## Blank lines and wrapping

Blank lines split code into paragraphs that can be skimmed by intent, such as "validate the arguments".

- A blank line goes either side of any member with a body, XML docs or attributes, or that spans several lines.
- Group consecutive single-line fields, constants or auto-properties by purpose, with a blank line between groups.
- Argument validation comes first, one paragraph per argument, then a blank line.
- Leave a blank line after a closing brace, and before the final `return` unless it pairs with the line above.
- Split calculations into paragraphs of one to three lines per step.
- Keep a declaration attached to the statement that consumes it. A one-line operation stays attached to a simple
  check of its result; separate a multi-line call or complex check with a blank line.
- A `//` comment starts a paragraph: blank line above, code directly below.
- Group `using` directives by root namespace (`System`, `Microsoft`, third-party, `Invicta`), separated by blank lines.
- Put each `where` constraint on its own line, and a primary constructor's base list on the next line.
- The closing parenthesis stays on the line of the last argument or parameter.

## Comments and documentation

- Every non-test type and member, including internal ones, has `///` XML docs; fields and trivial private
  constructors may go without. Implementations of interface members can use `<inheritdoc/>`.
- Comment a private member only where the information is not already obvious, because the name describes it or the
  implementation reads plainly. Prefer a name that needs no comment, and treat a comment longer than the member it
  describes as a sign it is restating the code rather than explaining it.
- Summaries start with a present-tense verb ("Gets…", "Creates…"). Boolean properties start "Gets a value
  indicating whether". Use `<see langword="null"/>` for keywords, and don't start exception docs with "Thrown if".
- Details that callers need go in `<remarks>`; notes for maintainers, such as rationale or "guarded by `_lock`",
  stay as `//` comments in the implementation.
- Document only what callers wouldn't already expect from the type's contract. Don't restate that a disposable object
  holds its resources until disposed, or that an implementation behaves like the framework's own; do point out where
  it differs.
- Public docs never `<see cref>` a private member; state the fact in prose instead.
- Trailing comments are a few words at most, such as units; anything longer goes on the line above.

## Tests

- Use NUnit with the constraint model (`Assert.That(actual, Is.EqualTo(expected))`), and NSubstitute only when
  something needs faking. Test projects reference `NUnit`, `NUnit3TestAdapter`, `NUnit.Analyzers` and
  `Microsoft.NET.Test.Sdk`.
- Test fixtures are `internal sealed class`, named `<Type>Tests` in folders mirroring the code under test. A class
  may have several fixtures when its tests need different setups, such as one run against several implementations.
  Tests are named `Method_Scenario_Expectation`.
- Group independent assertions in `using (Assert.EnterMultipleScope())`; use `[TestCase]` or `[TestCaseSource]`
  rather than near-duplicate tests.
- No "Arrange", "Act" or "Assert" comments, and no commented-out or ignored tests left behind.
- Put tests that depend on wall-clock timing in their own category, so they can be excluded on busy machines.
- Confirm the tests actually ran, from the test count, before reporting them passed.

## Native interop

- Follow Microsoft's [native interoperability best practices][interop]: `[LibraryImport]`, a class named after the
  DLL (`Kernel32`), native names for functions, parameters and constants, the closest native types, `SafeHandle` and
  `SetLastError = true`.
- Add `[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]` to each import, and suppress IDE1006 on the
  class with a `[SuppressMessage]`.
- Each native function's XML docs end with `<seealso href="…"/>` linking to its official documentation.

[interop]: https://learn.microsoft.com/dotnet/standard/native-interop/best-practices
