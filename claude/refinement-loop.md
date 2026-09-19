# The refinement loop

How a finished project is polished: a loop to repeat until a whole pass finds nothing. The aim is
Saint-Exupéry's: a pass succeeds when something is taken away, and adding anything must earn its place.

## 1. Re-read before touching anything

- **The rules:** `claude/CLAUDE.md`, `csharp.md` and `markdown.md` in this repository. They change, so read them
  again.
- **The code:** every file the pass could touch, in full. Never work from memory of an earlier pass.
- **A reading budget:** a few hundred lines in a sitting. Inspection studies find that faults spotted per line fall
  sharply as reading speeds up, so split a long pass across sittings and say which files this one covers.
- **A viewpoint:** read as one reader and name which, rotating it from pass to pass, because each finds what the
  others read past. The caller of the public API reads for argument checks, docs and awkward call sites; the
  maintainer six months on, with a bug report in hand, reads for rationale, names and testability; someone porting
  the code reads for assumptions about the platform, the culture and the data.
- **The state:** the working tree is clean, and the branch is the one this loop is running on.

## 2. Take a baseline

- **Build:** `dotnet build -c Release --no-incremental`, which must report 0 warnings. Incremental builds hide
  analyzer warnings.
- **Style:** `dotnet format --verify-no-changes --severity info`, which reports the IDE rules a build does not.
- **Tests:** `dotnet test -c Release --no-build`, and read the count, not just the word "Passed".
- **Record the numbers,** so the next pass can tell whether anything moved.

## 3. Look for one improvement, in this order

1. **Correctness:** races, broken invariants, doubtful assumptions, tests asserting the wrong thing.
2. **Design:** Ousterhout's red flags in the shape of a type or method — an interface nearly as complex as what it
   hides, one decision leaking into several places, structure that follows the order of execution rather than what
   each part knows, an API that exposes rare features to reach common ones, or a name that is hard to choose because
   the thing does too much.
3. **Subtraction:** code, comments, tests, docs or configuration that can go without losing anything.
4. **Reuse:** custom code the base libraries already provide.
5. **Behaviour:** divergence from the framework's own implementation of the same abstraction.
6. **Rules:** conformance with the guidelines, such as member order, documentation and test categories.
7. **Names:** anything whose name no longer says what it is for, is ambiguous without a comment, or carries words it
   does not need. Abbreviate only in private field names, and only where the abbreviation is understood here.
8. **Clarity:** shapes, and comments that give the rationale rather than restate the code.
9. **Performance:** only with a measurement, and only where it costs no readability.
10. **Documentation:** the README, XML docs and commit-worthy facts that no longer match the code.

Stop at the first candidate worth doing; one change per iteration.

## 4. Test the candidate before writing it

- **Ask what is lost.** If nothing is lost, prefer removal over any addition.
- **Find the fence before removing it:** search the history for why the code is there, such as `git log -S`, and for
  the tests that cover it. If no reason survives, that is the evidence for removing it.
- **Design it twice:** for anything larger than a local edit, sketch a second approach that differs in kind rather
  than in detail, and say why the chosen one wins.
- **Check the framework:** read dotnet/runtime's own source before claiming what .NET does.
- **Prove claims:** any performance, allocation or behaviour claim needs a scratch program in the scratchpad
  directory, never in the repository. Run it more than once, alternating variants, and use medians.
- **Reject speculation:** no option, abstraction or hook for a need that has not arrived.

## 5. Apply it

- **Scope:** change only what this improvement needs; note anything else for a later iteration.
- **Mechanics:** CRLF, no BOM, no trailing whitespace, 120 columns.

## 6. Verify it

- **Repeat the baseline commands,** and compare the numbers with the ones recorded in step 2.
- **Add the evidence** the change itself needs: a benchmark, an allocation count, a new test that fails without it.
- **Report honestly:** what was verified, and what is only expected.

## 7. Commit it

- **One commit per improvement,** with a British English message: an imperative subject line, then why it is an
  improvement and what evidence supports it.
- **One kind of change per commit:** behaviour or structure, never both, so that either can be read or reverted on
  its own. When an improvement needs both, commit the structural change first and say which kind each message covers.
- **Write the message to a BOM-free file** and use `git commit -F`, ending with the Co-Authored-By line.

## 8. Stop when a whole pass finds nothing

- **Finish the pass** even after a change: go round again from step 1.
- **Stop** when every one of the ten axes in step 3 yields nothing on a complete pass, and every viewpoint in step 1
  has had such a pass.
- **Record** the final baseline numbers and what was considered and rejected.
- **Keep a log** of each finding against the axis and viewpoint it came from, as Knuth did for the errors of TeX, so
  that a later project can tell which of them earn their place and which never fire.
