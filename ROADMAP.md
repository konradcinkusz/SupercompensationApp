# Roadmap

How this repository gets from where it is to a version worth handing to somebody else.
Milestones carry *what* and *when*; this file carries *why*, *in what order*, and *what
must not break*. It is written for a reader with no memory of the session that produced
it.

Tracker: [#18](https://github.com/konradcinkusz/SupercompensationApp/issues/18) — the
running log and the decision log live there as comments.

---

## 1. What "complete" means here

This is a .NET 8 Blazor WebAssembly single-page application that models the
supercompensation (hypercompensation) curve across Agile sprints: fatigue, recovery, and
a peak above baseline, with a configurable team and a progressive baseline. There is no
server, no database and no account. The whole output is a picture and a table.

A mature version of it:

1. **Computes the right curve, provably.** `Services/SupercompensationService.cs` is the
   product — every chart point, summary card and CSV row comes out of it. Its piecewise
   function and the aggregates built on it are pinned by unit tests covering phase
   boundaries, continuity at the joins, endpoint values and monotonicity. This is not
   generic test-coverage hygiene: a sign error or a swapped denominator in one of three
   branches produces a curve that still renders, still looks plausible, and is wrong.
   There is no exception to catch and no visual tell.

2. **Renders what it computes.** Phase bands, sprint boundary lines and axis labels agree
   with the curve, and are legible against the dark theme the application declares in
   `:root`.

3. **Can be opened by somebody without a .NET SDK.** A published static WebAssembly
   build, linked from the README. A visualisation tool that requires a toolchain to see
   has the wrong distribution shape.

4. **Keeps the user's input.** Configuration and team survive navigation and reload, held
   in state that a test can construct without instantiating a page component.

5. **Carries the estate's repository baseline** — CI that builds, tests and format-checks;
   secret scanning; SAST; dependency automation; CODEOWNERS and templates — scoped to
   what a client-only WASM app actually needs. Source:
   [`konradcinkusz/architecture-standards`](https://github.com/konradcinkusz/architecture-standards)
   → `docs/guides/REPO-BASELINE.md` §1 and §9, whose governing sentence is *"anything
   enforced only by human recall is not enforced."*

6. **Documents what it assumes**, not only what it does. Four things are true of the
   implementation today and stated nowhere: the 50%/80% phase split is hard-coded rather
   than configurable; performance jumps discontinuously at every sprint boundary; team
   weight is a **sum**, so headcount multiplies every number on the chart; and
   `DeliveryValue` is an integral, so it scales with sprint length while the peak and min
   beside it on the same card do not.

### What is deliberately not in scope

- A backend, an account system, or persistence beyond the browser.
- Changing the mathematical model. The curve is documented, tested, and — where it is
  surprising — explained. It is not redesigned.
- Localisation beyond the existing Polish UI. Culture handling is fixed where it corrupts
  data; the interface stays Polish.
- Migration off .NET 8 or off Chart.js.

---

## 2. Phases

GitHub milestones could not be created from the session that wrote this plan (see
[§6](#6-execution-policy)), so **phases are carried as `phase-N` labels**. `label:phase-2
is:open` is the query that would otherwise be a milestone view. Converting them to real
milestones later is a mechanical change and loses nothing.

| Phase | Due | Issues | Goal |
|---|---|---|---|
| **1 — Pipeline and baseline** | 2026-09-15 | [#1](https://github.com/konradcinkusz/SupercompensationApp/issues/1), [#2](https://github.com/konradcinkusz/SupercompensationApp/issues/2), [#3](https://github.com/konradcinkusz/SupercompensationApp/issues/3), [#4](https://github.com/konradcinkusz/SupercompensationApp/issues/4), [#5](https://github.com/konradcinkusz/SupercompensationApp/issues/5) | Make a pushed commit verifiable at all |
| **2 — Correctness and tests** | 2026-09-29 | [#6](https://github.com/konradcinkusz/SupercompensationApp/issues/6), [#7](https://github.com/konradcinkusz/SupercompensationApp/issues/7), [#8](https://github.com/konradcinkusz/SupercompensationApp/issues/8), [#9](https://github.com/konradcinkusz/SupercompensationApp/issues/9), [#10](https://github.com/konradcinkusz/SupercompensationApp/issues/10), [#11](https://github.com/konradcinkusz/SupercompensationApp/issues/11) | Pin the model; fix the three known defects |
| **3 — Architecture and state** | 2026-10-13 | [#12](https://github.com/konradcinkusz/SupercompensationApp/issues/12), [#13](https://github.com/konradcinkusz/SupercompensationApp/issues/13), [#14](https://github.com/konradcinkusz/SupercompensationApp/issues/14) | State that a test can reach and a reload survives |
| **4 — Delivery and documentation** | 2026-10-27 | [#15](https://github.com/konradcinkusz/SupercompensationApp/issues/15), [#16](https://github.com/konradcinkusz/SupercompensationApp/issues/16), [#17](https://github.com/konradcinkusz/SupercompensationApp/issues/17) | Publish it; say what it assumes |

Two-week phases, one maintainer. The cadence is an **assumption, not a measurement**: the
repository's history is two commits made on the same day, which tells you nothing about
sustainable pace. Revise it against real throughput rather than treating it as a
commitment.

---

## 3. Why this order

**Phase 1 first, and not by convention.** This repository has no `.github/` directory at
all — nothing compiles a pushed commit, so a change that does not build is
indistinguishable from one that does. Every later phase's acceptance criteria are
statements about a green build; without CI they are statements about nothing. The
force-merge rule in [§6](#6-execution-policy) is also meaningless until there are checks
capable of failing.

**Within Phase 2**, the harness precedes the assertions: [#6](https://github.com/konradcinkusz/SupercompensationApp/issues/6)
creates the test project and wires `dotnet test` into CI, and [#7](https://github.com/konradcinkusz/SupercompensationApp/issues/7)
fills it. [#8](https://github.com/konradcinkusz/SupercompensationApp/issues/8) and
[#9](https://github.com/konradcinkusz/SupercompensationApp/issues/9) sit outside that
dependency deliberately — they are rendering defects in `wwwroot/js/chart-interop.js` with
no unit-testable surface, verified by looking at the chart, so blocking them on a C# test
project would be ceremony.

The reason [#6](https://github.com/konradcinkusz/SupercompensationApp/issues/6) does not
simply come first, before CI: `dotnet test` over a solution containing only a Blazor
WebAssembly project fails with *"no test source files were specified"*. So
[#1](https://github.com/konradcinkusz/SupercompensationApp/issues/1) ships a CI workflow
with build and format only, and [#6](https://github.com/konradcinkusz/SupercompensationApp/issues/6)
adds the test step alongside the project it needs. Each PR is then internally coherent
and neither has to weaken a gate to be green.

**Phase 3 depends on Phase 2's harness, not its fixes.** Static mutable state on a page
component is untestable in the specific sense that xUnit parallelises test classes and
`Index.Config` is shared across the process, so
[#12](https://github.com/konradcinkusz/SupercompensationApp/issues/12) becomes urgent the
moment tests exist rather than before.

**Phase 4 last**, because [#16](https://github.com/konradcinkusz/SupercompensationApp/issues/16)
documents behaviour that [#9](https://github.com/konradcinkusz/SupercompensationApp/issues/9),
[#10](https://github.com/konradcinkusz/SupercompensationApp/issues/10),
[#11](https://github.com/konradcinkusz/SupercompensationApp/issues/11) and
[#15](https://github.com/konradcinkusz/SupercompensationApp/issues/15) change, and
[#17](https://github.com/konradcinkusz/SupercompensationApp/issues/17) documents the
repository as it ends up. Writing either earlier means writing it twice.

---

## 4. Dependencies

Every `Blocked by` in the issue set, in one place. An issue not listed here is
parallel-safe.

| Issue | Blocked by | Why |
|---|---|---|
| [#6](https://github.com/konradcinkusz/SupercompensationApp/issues/6) | [#1](https://github.com/konradcinkusz/SupercompensationApp/issues/1) | There is no `ci.yml` to add a test step to |
| [#7](https://github.com/konradcinkusz/SupercompensationApp/issues/7) | [#6](https://github.com/konradcinkusz/SupercompensationApp/issues/6) | Needs the test project |
| [#10](https://github.com/konradcinkusz/SupercompensationApp/issues/10) | [#6](https://github.com/konradcinkusz/SupercompensationApp/issues/6) | Culture assertions need the test project |
| [#11](https://github.com/konradcinkusz/SupercompensationApp/issues/11) | [#6](https://github.com/konradcinkusz/SupercompensationApp/issues/6) | Validation is asserted, not eyeballed |
| [#12](https://github.com/konradcinkusz/SupercompensationApp/issues/12) | [#6](https://github.com/konradcinkusz/SupercompensationApp/issues/6) | The proof it works is a test constructing the service directly |
| [#14](https://github.com/konradcinkusz/SupercompensationApp/issues/14) | [#12](https://github.com/konradcinkusz/SupercompensationApp/issues/12) | Persistence belongs to the state service |
| [#16](https://github.com/konradcinkusz/SupercompensationApp/issues/16) | [#9](https://github.com/konradcinkusz/SupercompensationApp/issues/9) | Should document the corrected chart |

Soft ordering, not blocking: [#8](https://github.com/konradcinkusz/SupercompensationApp/issues/8)
and [#9](https://github.com/konradcinkusz/SupercompensationApp/issues/9) both edit
`chart-interop.js`; [#12](https://github.com/konradcinkusz/SupercompensationApp/issues/12)
and [#13](https://github.com/konradcinkusz/SupercompensationApp/issues/13) both edit the
team table markup. Whichever lands second rebases.

---

## 5. Protected paths

Files whose breakage compromises **every later PR** rather than one feature. A broken CSS
rule is contained; a broken validator or format gate turns every subsequent PR into
three-retries-and-force-merge, which is CI ceasing to exist.

- `.github/workflows/**`
- `SupercompensationApp.csproj`, `SupercompensationApp.sln`
- `Directory.Build.props`, `Directory.Packages.props` — once [#6](https://github.com/konradcinkusz/SupercompensationApp/issues/6) adds them
- `.editorconfig` — the format gate reads it, so it decides whether every PR is green
- `.gitleaks.toml`

`.github/CODEOWNERS` names these explicitly, so the list exists in two places on purpose:
here with its reasoning, there where GitHub enforces it.

---

## 6. Execution policy

- **One issue = one PR.** Never batched.
- **Never merge without CI having actually run** on the pushed branch — with the two
  bootstrap exceptions below, which are recorded rather than assumed.
- **Retry cap: 3 fix attempts per PR.** A fixed number, not renegotiable mid-run.
- **After 3 code-caused CI failures:**
  - the diff touches **no** protected path → force-merge, say so explicitly in the PR and
    the tracker, and open `Fix CI: <title>` in the same phase carrying the last failure
    excerpt, labelled `tech-debt`;
  - the diff touches a protected path → **do not merge**. Leave the PR open, label PR and
    issue `blocked`, comment with the diagnosis and all three attempts, move on.
- **Infrastructure-caused failure** (auth, quota, outage, runner unavailable — the log
  shows no code path): do not spend the retry cap. Re-run once; if it still fails for the
  same infra reason, enter pipeline-degraded mode — force-merge, open **one**
  `Fix CI: pipeline` issue, and for subsequent PRs re-run once then force-merge until a
  check passes normally. The protected-path rule does not apply here: an infra failure
  says nothing about the code.
- **After the second force-merge in one phase**, open `Review: phase N specs — N
  force-merges` in that phase and carry on. It is a signal for later review, not a brake.

### Bootstrap exceptions

Two PRs necessarily merge without checks, because the checks do not exist yet:

1. This roadmap PR. There is no workflow in the repository at the point it is opened.
2. Nothing else — [#1](https://github.com/konradcinkusz/SupercompensationApp/issues/1)'s
   own PR *does* get CI, because a `pull_request`-triggered workflow added by a PR from a
   branch in the same repository runs on that PR.

Both are recorded in the tracker's decision log rather than being treated as ordinary
merges.

### Branching

The session that executes this roadmap is constrained to a single named branch, so PRs are
sequential: the branch is reset from `master` after each merge and reused for the next
issue. That is why the PR list shows one open PR at a time rather than several. A human
maintainer working normally should use one branch per issue.

### Milestones

`gh` is unavailable in the executing environment and `api.github.com` returns 403 through
its proxy; the GitHub MCP server that *is* available exposes no milestone endpoint. Phases
are therefore `phase-N` labels. If you are reading this with working milestone access, the
five-minute improvement is to create four milestones from the table in
[§2](#2-phases) and reassign the issues.
