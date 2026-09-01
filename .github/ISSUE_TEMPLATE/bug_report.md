---
name: Bug report
about: Something the app does that it should not
title: ""
labels: bug
assignees: ""
---

## What happened



## What you expected instead



## Sprint parameters

<!--
This app's defects vary with its inputs — a curve or a chart that is fine at
SprintDuration = 10 can be wrong at 7, because several bugs are ratios rather than
constants. Please give the values from the Konfiguracja tab, or say "defaults".
-->

| | |
|---|---|
| Czas trwania sprintu | |
| Liczba sprintów | |
| Przyrost baseline | |
| Początkowy baseline | |
| Głębokość zmęczenia | |
| Szczyt superkompensacji | |
| Liczba członków zespołu | |

## Browser and locale

<!--
Locale is not a formality here. Blazor WebAssembly takes its culture from the browser,
and several known defects depend on it — a decimal comma changes how numbers are parsed
from the inputs and written into the exported CSV. If you are not sure, run this in the
browser console and paste the result:

    navigator.language, Intl.NumberFormat().resolvedOptions().locale
-->

- Browser and version:
- Locale:

## Anything else

<!-- A screenshot, the exported CSV, or a console error. -->
