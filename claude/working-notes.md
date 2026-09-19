# Working notes

What the rules do not say: how to verify work on this setup, and the traps that have already cost time. Not imported,
so read it when starting work on a .NET repository.

## Verifying a change

- **Build:** `dotnet build -c Release --no-incremental`. An incremental build reports no analyzer warnings, so only a
  clean build proves there are none.
- **Style:** `dotnet format --verify-no-changes --severity info`. IDE rules only appear when their severity is set, so
  check a specific one directly, as in `dotnet format style --diagnostics IDE0005 --severity info`.
- **Tests:** read the count, not the word "Passed". Run them in Debug as well when `Debug.Assert` carries part of a
  contract, because Release compiles the assertions away.

## Analyzer suppressions

- **Prove each one is still needed** by deleting them all, rebuilding, and restoring only those that reappear. CA1707
  and CA2213 had both stopped firing in Invicta.Time long before anyone noticed.
- **CA1001 and CA2213 follow an ownership heuristic:** they fire when the type itself constructs the disposable and
  stay quiet when a factory or a parameter hands it over, so moving a `new` can turn them on or off.
- **Usually needed:** CA1416 and CA2007 in tests, samples and benchmarks; CA5394 where a seeded `Random` gives
  reproducible data; CA1515 where a framework requires public types.

## Tests

- **NUnit runs tests one at a time, in alphabetical order,** unless told otherwise. Timing-sensitive fixtures warm up
  in `[OneTimeSetUp]` rather than depending on that order.
- **NUnit2045** reports independent assertions outside `Assert.EnterMultipleScope`, and fails the style check.
- **Prove a new test has teeth** by mutating the code it guards and watching it fail. A test that passes for the wrong
  reason is worse than none: deleting a period assignment once made a test pass, because every timer became one-shot.

## Editing files on Windows

- **`sed -i` and `perl -CSD -i` rewrite a file with LF endings.** Plain `perl -0pi` keeps CRLF; either way, check with
  `grep -c $'\r'` against the line count before finishing.
- **PowerShell 5.1 adds a BOM** to text piped into a file, so write commit messages with a Bash heredoc and pass them
  to `git commit -F`.

## Habits worth keeping

- **Put the evidence in the commit message:** the measurement, the test count, what was rejected and why. "A median
  one-shot of 1.51-1.53 ms either way" is worth more a year later than "no regression".
- **One improvement per commit,** however many turn up in one sitting.
- **Say when a request conflicts with the rules** instead of choosing silently. The rules on comments and on naming
  both started as a contradiction worth raising.
- **Alternate measurements** between the old and new code, take medians, and discard the first runs after a build.
