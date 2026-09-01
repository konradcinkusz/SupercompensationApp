## What changed



## Why



## How it was verified

<!--
What you actually ran or looked at, not what should pass. Several defects in this
repository were invisible in a diff and visible only on the rendered page — the chart
title painted in its own background colour is the standing example. If a change affects
what the chart looks like, say you looked at it.
-->



## Checklist

- [ ] Closes an issue (`Closes #N`), or says why it does not
- [ ] **Touches a protected path** — `.github/**`, the `.csproj`/`.sln`,
      `Directory.*.props`, `.editorconfig`, `.gitleaks.toml`. If so, this cannot be
      merged over a red CI run; see `ROADMAP.md` §5–6.
- [ ] CI is green, or the failure is diagnosed in a comment
