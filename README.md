# 📈 Supercompensation Model — Blazor WebAssembly

Interaktywna aplikacja webowa do modelowania hiperkompensacji w sprintach Agile.
Wizualizacja krzywej zmęczenia → regeneracji → superkompensacji z konfigurowalnymi parametrami zespołu i sprintu.

## Roadmap

Plan rozwoju repozytorium — definicja „gotowego”, fazy, kolejność prac, zależności
i ścieżki chronione — znajduje się w [`ROADMAP.md`](ROADMAP.md).
Bieżący postęp: [tracker #18](https://github.com/konradcinkusz/SupercompensationApp/issues/18).

## Uruchomienie

**Wersja online:** https://konradcinkusz.github.io/SupercompensationApp/

Publikowana automatycznie z gałęzi `master`. Nie wymaga instalacji .NET — wystarczy
przeglądarka z obsługą WebAssembly i dostęp do sieci (biblioteki wykresu ładowane są z CDN).

Lokalnie:

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

Krzywa superkompensacji dla jednego sprintu, gdzie `s` ∈ [0, 1] to postęp sprintu:

| Faza               | `s`        | Wzór              | `t` w tym wzorze     |
|---------------------|------------|-------------------|----------------------|
| Fatigue             | 0 → 50%    | `−D · t²`         | `t = s / 0,5`        |
| Recovery            | 50 → 80%   | `−D · (1−t)²`     | `t = (s − 0,5) / 0,3`|
| Supercompensation   | 80 → 100%  | `P · sin(t · π/2)`| `t = (s − 0,8) / 0,2`|

**`t` jest inne w każdym wierszu** i biegnie od 0 do 1 *wewnątrz danej fazy* — to nie
jest ten sam `t` co postęp sprintu. Rozróżnienie ma znaczenie: przy `s = 0,5` wzór
`−D · s²` dałby `−6,25`, podczas gdy dołek krzywej wynosi dokładnie `−D = −25`.

Gdzie: `D` = głębokość zmęczenia (parametr), `P` = szczyt superkompensacji (parametr).

**Granice faz — 50% i 80% — są stałymi modelu, nie parametrami.** Są zapisane w kodzie
jako właściwości bez settera (`SprintConfiguration.FatiguePhaseEnd`,
`RecoveryPhaseEnd`) i nie da się ich zmienić bez edycji źródeł.

**Progresywny baseline:** `B(n) = B₀ + n × ΔB`, gdzie `n` liczone od zera.

**Wydajność:** `Performance(d) = (B(sprint) + deviation(d)) × W_team`

### Zakresy parametrów

Wymuszane przez aplikację — wartość spoza zakresu blokuje przycisk generowania i jest
wypisana pod nim. Ograniczenia `min`/`max` w HTML są jedynie podpowiedzią i nie
powstrzymują wklejenia wartości, dlatego walidacja żyje w modelu.

| Parametr | Zakres |
|---|---|
| Czas trwania sprintu | 5 – 30 |
| Liczba sprintów | 1 – 20 |
| Przyrost baseline | 0 – 50 |
| Początkowy baseline | 50 – 200 |
| Głębokość zmęczenia | 5 – 50 |
| Szczyt superkompensacji | 5 – 40 |

## Założenia i ograniczenia

Model jest prosty i przekonująco wygląda na wykresie. To jest lista rzeczy, których
wykres **nie mówi**, a bez których łatwo odczytać go źle.

**1. Waga zespołu to SUMA, nie średnia.** `CalculateTeamWeight` zwraca sumę wag
członków, więc dodanie osoby mnoży *każdą* liczbę na wykresie. Domyślny
siedmioosobowy zespół daje wagę `6,35`; ten sam model dla jednej osoby o wadze `1,0`
daje liczby 6,35 raza mniejsze. **Porównując dwie konfiguracje zespołu, różnicę w
liczebności odczytasz jako różnicę w produktywności.** Oś Y jest opisana „wydajność
zespołu (ważona)” i to jest właśnie ta ważona suma.

**2. Krzywa skacze na granicy każdego sprintu.** Sprint kończy się `P` powyżej swojej
linii bazowej, a następny zaczyna się `0` powyżej linii wyższej o `ΔB`. Przy
domyślnych wartościach (`P = 18`, `ΔB = 15`) wydajność **spada o 19,05** przez noc:

```
koniec sprintu 1   = (100 + 18) × 6,35 = 749,30
początek sprintu 2 = (115 +  0) × 6,35 = 730,25
```

To jest **świadoma własność modelu**, nie przeoczenie: superkompensacja wygasa, jeśli
nie zostanie wykorzystana jako nowa baza. Ale z samego wykresu nie da się odróżnić
decyzji od błędu, więc jest zapisana tutaj.

**3. `Delivery value` to całka, a nie tempo.** To pole powierzchni między krzywą a
linią bazową w fazie superkompensacji, w jednostkach *wydajność × dni*. Na karcie
podsumowania stoi obok `Peak` i `Min`, które są wartościami chwilowymi — trzy liczby w
jednym miejscu, z których dwie są tempem, a jedna akumulacją. **Podwojenie długości
sprintu podwaja `Delivery value` i nie zmienia pozostałych dwóch:**

| Czas trwania sprintu | Delivery value | Na dzień |
|---|---|---|
| 5 | 74,19 | 14,84 |
| 10 | 148,37 | 14,84 |
| 20 | 296,74 | 14,84 |
| 30 | 445,11 | 14,84 |

**4. Granice faz są stałe.** Patrz wyżej: 50% i 80% nie są parametrami. Tabela modelu
w starszych wersjach tego pliku prezentowała je obok `D` i `P`, co sugerowało pokrętło,
którego nie ma.

**5. Aplikacja wymaga sieci.** Chart.js i wtyczka `chartjs-plugin-annotation` są
ładowane z `cdn.jsdelivr.net` (z sumami kontrolnymi SRI). Bez dostępu do sieci
aplikacja uruchomi się, ale wykres się nie narysuje. Nie działa offline.

**Czego ten model nie jest.** Nie jest wynikiem pomiaru żadnego zespołu. To wizualizacja
kształtu, o którym mówi teoria superkompensacji, z parametrami, które ustawiasz sam —
przydatna do rozmowy o tym kształcie, a nie do przewidywania czyjejkolwiek wydajności.

## Technologie i zależności

- .NET 8 Blazor WebAssembly
- JS Interop dla wykresu i dla `localStorage`
- Czysty CSS (dark theme, responsive) — bez frameworka

Pełna lista zależności zewnętrznych. Liczba w tej tabeli, która rośnie w diffie, jest
pytaniem, które ktoś może zadać; tabela, której nikt nie aktualizuje, jest widocznie
nieaktualna.

**NuGet** — wersje w `Directory.Packages.props`, aktualizowane przez Dependabota:

| Pakiet | Wersja | Po co |
|---|---|---|
| `Microsoft.AspNetCore.Components.WebAssembly` | 8.0.12 | środowisko uruchomieniowe aplikacji |
| `Microsoft.AspNetCore.Components.WebAssembly.DevServer` | 8.0.12 | tylko `dotnet run` (`PrivateAssets`) |
| `Microsoft.NET.Test.Sdk` | 17.11.1 | tylko testy |
| `xunit` | 2.9.2 | tylko testy |
| `xunit.runner.visualstudio` | 2.8.2 | tylko testy |
| `coverlet.collector` | 6.0.2 | tylko testy |

**CDN** — ładowane w `wwwroot/index.html`. **Dependabot ich nie widzi**: nie czyta
znaczników `<script>` w HTML, więc nic nie zgłosi, gdy się zestarzeją. Wersję i sumę
kontrolną trzeba zmieniać razem — błędna suma sprawia, że przeglądarka po cichu odrzuca
skrypt, a objawem jest pusty wykres, nie błąd.

| Skrypt | Wersja | `integrity` (sha384) |
|---|---|---|
| `chart.js` (`dist/chart.umd.js`) | 4.4.1 | `dug+JxfBvklEQdJ4AYuBBAIScUz0bVN73xpy273gcAwHjb3qI0fXmuYNaNfdyYJG` |
| `chartjs-plugin-annotation` | 3.0.1 | `oNtu+d18330MVFpltUTve1DatxCkkctlpA2AC3GulbVFOSqhHdDat3qHse/Lbuek` |

`chart.js` wskazuje `dist/chart.umd.js`, a nie `chart.umd.min.js`: wersja 4.4.1 **nie
publikuje** pliku zminifikowanego — `.min.js` generuje jsDelivr w locie, więc suma
kontrolna dotyczyłaby bajtów, których autor pakietu nigdy nie opublikował. `chart.umd.js`
i tak jest budową zminifikowaną, tylko bez `.min` w nazwie.

Trzecia zależność niewidoczna dla Dependabota: obraz `zricethezav/gitleaks:v8.28.0`
przypięty wewnątrz kroku `run:` w `.github/workflows/secret-scan.yml`.

## Rozwój

Szczegóły w [`CONTRIBUTING.md`](CONTRIBUTING.md) — jak uruchomić, jak testować i jak
odtworzyć każdy job CI lokalnie.

Na każdy pull request uruchamiane są:

| Job | Co sprawdza |
|---|---|
| **CI / Build** | `dotnet restore`, `build -c Release`, `dotnet test` |
| **CI / Format check** | `dotnet format --verify-no-changes --severity error` |
| **Secret scan / gitleaks** | całą historię, nie tylko drzewo robocze |
| **CodeQL** | `csharp` oraz `javascript-typescript` |
| **Deploy GitHub Pages / Build** | że artefakt publikacji faktycznie się składa |

Ścieżki chronione — pliki, których uszkodzenie psuje **każdy** kolejny pull request — są
wypisane w [`ROADMAP.md`](ROADMAP.md) §5 i w `.github/CODEOWNERS`.

## Wymagania

- .NET 8 SDK
- Przeglądarka z obsługą WebAssembly
