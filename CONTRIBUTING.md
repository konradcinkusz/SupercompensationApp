# Contributing

Small repository, short guide. The one thing worth reading before you push is
[Reproducing CI locally](#reproducing-ci-locally) — the format gate is the check most
likely to fail on a first contribution and the least obvious to run.

## Prerequisites

- **.NET 8 SDK.** The project targets `net8.0` and CI pins `8.0.x`. A newer SDK will
  build it, but pinning is what makes a green build here mean the same thing next month.
- **A browser with WebAssembly** — anything current.
- **Network access at runtime.** Chart.js and `chartjs-plugin-annotation` load from
  `cdn.jsdelivr.net`. The application starts offline; the chart does not draw. This is
  not obvious from the source and it is why it is the second item on this list.
- **Docker**, only if you want to run the secret scanner locally. Everything else needs
  the SDK alone.

Nothing here needs a credential, a token or an environment variable. This application has
none, and it should stay that way.

## Running it

```bash
dotnet restore
dotnet run
```

`http://localhost:5171`, or `https://localhost:7171`.

A live build is published from `master`:
https://konradcinkusz.github.io/SupercompensationApp/

## Running the tests

```bash
dotnet test SupercompensationApp.sln
```

Tests live in `tests/SupercompensationApp.Tests/`. What is there and why:

| File | Pins |
|---|---|
| `SupercompensationCurveTests` | phase boundaries, continuity at the joins, endpoints, monotonicity, the aggregates |
| `SprintConfigurationValidationTests` | every parameter range, both directions, plus NaN and the infinities |
| `CsvExporterTests` | the export is byte-identical under `pl-PL`, `de-DE` and `en-US` |
| `AppStateServiceTests` | state, staleness, team membership, change notification |
| `StatePersistenceTests` | the storage round trip and every fail-soft path |
| `PageComponentTests` | the four `.razor` components: the restore reaching the input, every page re-rendering on `OnChange`, navigation resolving against `<base href>`, and the two `disabled` guards |

Two conventions in these tests, both load-bearing:

- **Assert the invariant, not the observation.** A measured floating-point residual is a
  property of the machine that measured it. `SupercompensationCurveTests` derives its
  continuity bound from the curve's own maximum slope rather than committing a number,
  and the derivation is in a comment so the next person can check it.
- **A check that cannot fail is not a check.** `CsvExporterTests` contains a test that
  asserts `pl-PL` really does format a decimal with a comma — because without ICU,
  `new CultureInfo("pl-PL")` silently yields invariant behaviour and every other test in
  that file would pass while never exercising a second culture.

  The same rule shapes `PageComponentTests`. Its navigation test supplies a
  `NavigationManager` whose base URI carries a **path**, because under `http://localhost/`
  the correct `NavigateTo("chart")` and the defect `NavigateTo("/chart")` resolve to the
  same URI — a test against the default base passes either way and proves nothing. Every
  test in that file was confirmed red by re-introducing the defect it guards.
- **Test the component, not only the service.** The two defects the browser test found in
  #45 both lived in a `.razor` file while the services beneath them were correct, so all 48
  service tests passed throughout. `AppStateService` raises `OnChange` so that pages learn
  about a restore; whether a given page *subscribes* is a fact about the page.

## Reproducing CI locally

**Every command below is the one CI runs**, minus flags that exist only to produce CI
artifacts — the test command drops `--logger trx` and `--results-directory`, which you do
not want locally.

That matters more than it sounds. These are not commands somebody ran once on their
machine and wrote down: they are executed on every push, so if one of them drifts, CI goes
red and this file is wrong in a way somebody will notice. A contributing guide with an
untested command is the same defect as a committed-but-never-executed config.

| CI check | Run it locally |
|---|---|
| **CI / Build** | `dotnet restore SupercompensationApp.sln`<br>`dotnet build SupercompensationApp.sln --no-restore --configuration Release`<br>`dotnet test SupercompensationApp.sln --no-build --configuration Release` |
| **CI / Format check** | `dotnet format SupercompensationApp.sln --verify-no-changes --severity error` |
| **Secret scan / gitleaks** | `docker run --rm -v "$PWD:/repo" -w /repo zricethezav/gitleaks:v8.28.0 git /repo --config=/repo/.gitleaks.toml --redact --no-banner --verbose` |
| **Deploy GitHub Pages / Build** | `dotnet publish SupercompensationApp.csproj -c Release -o publish` |
| **CodeQL** | Not reproducible locally without the CodeQL CLI; it runs on every pull request. |

### If the format check fails

`--verify-no-changes` tells you *which* files would change and roughly where, which is
enough to find a stray space and not enough to fix a reindented block. Apply the fix and
read the diff:

```bash
dotnet format SupercompensationApp.sln --severity error
git diff
```

CI does exactly this in an `if: failure()` step, so a red run already carries the diff it
wants — check the log before reproducing it.

**Check whose formatter is complaining before you apply anything.** `dotnet format` is part
of the SDK, and different SDK feature bands format differently. On an Ubuntu-packaged
`dotnet-sdk-8.0` (8.0.130 at the time of writing) this command reports 18 `WHITESPACE`
errors in `Services/CsvExporter.cs` and `CsvExporterTests.cs` **on a clean checkout of
`master`**, while the same command is green in CI, which resolves `8.0.x` to a different
band. Applying those and committing them would reformat two files nobody touched and turn
CI red the other way.

So when the check fails locally, first run it on a clean tree. If the same files complain
there, it is your SDK and not your change, and the fix is to leave them alone — CI is the
authority, because CI is what gates the merge.

**Two things `dotnet format` does not cover**, so a green format check is not a claim
about them: it operates on Roslyn compilation units, so the `.razor` files and
`wwwroot/js/*.js` are untouched by it. The JavaScript is analysed by CodeQL and driven by
the browser test; the Razor is *tested* by `PageComponentTests` and driven by the browser
test, but its formatting is reviewed by people.

## Protected paths

These gate every *other* pull request, so breaking one is not contained. A change to any
of them is not force-merged over a red CI run — see [`ROADMAP.md`](ROADMAP.md) §5–6.

- `.github/workflows/**`
- `SupercompensationApp.csproj`, `SupercompensationApp.sln`
- `Directory.Build.props`, `Directory.Packages.props`
- `.editorconfig` — the format gate reads it, so it decides whether every PR is green
- `.gitleaks.toml`

`.github/CODEOWNERS` names the same set, so GitHub asks for a review on them.

Two of these have a trap worth knowing in advance:

- **`.editorconfig` entries are assertions about every existing file.** The first version
  of it set `csharp_preserve_single_line_statements = false`, which is not the Roslyn
  default and contradicted `GetPhase`'s three parallel guard clauses. CI rejected it. The
  fix was the setting, not the source.
- **Package versions live in `Directory.Packages.props`**, not in the `.csproj`. A
  `Version=` attribute on a `PackageReference` is an error under central package
  management.

## Pull requests

One issue, one pull request. Conventions taken from what this repository's merged history
actually shows:

- **Imperative subject, roughly 40–85 characters**, naming the change rather than the
  area: *Make the CSV export culture-invariant*, not *CSV fixes*.
- Where a subject names a defect, it says what was wrong: *Give the chart a linear x axis:
  phase bands and sprint boundaries were misplaced.*
- **The body explains why, and what was measured.** This codebase has a run of defects
  that were invisible in a diff — a chart title painted in its own background colour, a
  CSV that opens in every spreadsheet without an error, phase bands misplaced by a factor
  of five. "How it was verified" is the section reviewers need most.
- `Closes #N`.
- The PR template asks whether the change touches a protected path. It matters; answer it.

## Reporting a bug

Use the issue template — it asks for two things a generic one would not, and both earn
their place:

- **The sprint parameters.** Several defects here are *ratios* rather than constants and
  are invisible at one configuration while wrong at another.
- **Your locale.** Blazor WebAssembly takes its culture from the browser, and a decimal
  comma has already broken the CSV export once. The template gives the console snippet
  that reports it.
