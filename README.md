# 📈 Supercompensation Model — Blazor WebAssembly

Interaktywna aplikacja webowa do modelowania hiperkompensacji w sprintach Agile.
Wizualizacja krzywej zmęczenia → regeneracji → superkompensacji z konfigurowalnymi parametrami zespołu i sprintu.

## Roadmap

Plan rozwoju repozytorium — definicja „gotowego”, fazy, kolejność prac, zależności
i ścieżki chronione — znajduje się w [`ROADMAP.md`](ROADMAP.md).
Bieżący postęp: [tracker #18](https://github.com/konradcinkusz/SupercompensationApp/issues/18).

## Uruchomienie

```bash
cd SupercompensationApp
dotnet restore
dotnet run
```

Aplikacja uruchomi się na `http://localhost:5171`

## Struktura aplikacji

### 3 zakładki:

1. **⚙️ Konfiguracja** — Parametry sprintu + edycja zespołu (jak Excel)
   - Czas trwania sprintu, liczba sprintów, przyrost baseline
   - Głębokość zmęczenia i szczyt superkompensacji
   - Tabela zespołu: dodawanie/usuwanie członków, role, wagi

2. **📊 Wykres** — Interaktywny wykres hiperkompensacji (Chart.js)
   - Kolorowe fazy: Fatigue (czerwony) → Recovery (żółty) → Supercompensation (zielony)
   - Linia bazowa (baseline) ze schodkowym wzrostem
   - Podsumowania sprintów: peak, min, delivery value
   - Eksport do CSV

3. **📋 Dane** — Tabela dzień-po-dniu z filtrowaniem
   - Filtrowanie po sprincie i fazie
   - Odchylenie od baseline, wydajność ważona

## Model matematyczny

Krzywa superkompensacji dla jednego sprintu (znormalizowane dni 0→1):

| Faza               | Dni (%)  | Wzór                        |
|---------------------|----------|------------------------------|
| Fatigue             | 0 → 50% | `−D · t²`                   |
| Recovery            | 50 → 80%| `−D · (1−t)²`               |
| Supercompensation   | 80 → 100%| `P · sin(t · π/2)`         |

Gdzie: `D` = głębokość zmęczenia, `P` = szczyt superkompensacji

**Progresywny baseline:** `B(n) = B₀ + n × ΔB`

**Wydajność:** `Performance(d) = (B(sprint) + deviation(d)) × W_team`

## Technologie

- .NET 8 Blazor WebAssembly
- Chart.js 4.4 + chartjs-plugin-annotation
- JS Interop dla wykresów
- Czysty CSS (dark theme, responsive)

## Wymagania

- .NET 8 SDK
- Przeglądarka z obsługą WebAssembly
