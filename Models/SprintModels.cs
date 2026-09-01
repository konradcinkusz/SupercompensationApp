namespace SupercompensationApp.Models;

public class SprintConfiguration
{
    // The permitted ranges. These are the same numbers the Konfiguracja tab advertises
    // in its min/max attributes — but `min` and `max` on an <input> are validation
    // HINTS: they do not stop a value being typed or pasted, and @bind performs no
    // range check of its own. Holding the range here rather than only in the markup is
    // what makes it enforceable by something other than the browser's goodwill.
    public const int MinSprintDuration = 5;
    public const int MaxSprintDuration = 30;
    public const int MinNumberOfSprints = 1;
    public const int MaxNumberOfSprints = 20;
    public const double MinBaselineIncrement = 0.0;
    public const double MaxBaselineIncrement = 50.0;
    public const double MinInitialBaseline = 50.0;
    public const double MaxInitialBaseline = 200.0;
    public const double MinFatigueDepth = 5.0;
    public const double MaxFatigueDepth = 50.0;
    public const double MinSupercompensationPeak = 5.0;
    public const double MaxSupercompensationPeak = 40.0;

    public int SprintDuration { get; set; } = 10;
    public int NumberOfSprints { get; set; } = 3;
    public double BaselineIncrement { get; set; } = 15.0;
    public double InitialBaseline { get; set; } = 100.0;
    public double FatigueDepth { get; set; } = 25.0;
    public double SupercompensationPeak { get; set; } = 18.0;

    // Phases as fractions of sprint duration.
    //
    // Note that these are expression-bodied with no setter: 50% and 80% are FIXED
    // CONSTANTS of this model, not parameters, and cannot be configured without editing
    // source. The README's table presents them alongside D and P as though they were
    // knobs — see issue #16.
    public double FatiguePhaseEnd => 0.5;   // 50% of sprint
    public double RecoveryPhaseEnd => 0.8;  // 80% of sprint
    // Remaining 20% = supercompensation

    /// <summary>
    /// Returns one message per constraint this configuration violates, empty when it is
    /// usable.
    ///
    /// The failure this exists to stop is not an exception, it is a SILENT one. With
    /// SprintDuration = 0, `dayInSprint / config.SprintDuration` is 0/0, so every
    /// deviation is NaN, every comparison against NaN is false, the summaries keep the
    /// double.MinValue and double.MaxValue they were initialised with, and the chart
    /// renders blank with no error anywhere.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        Check(problems, SprintDuration, MinSprintDuration, MaxSprintDuration,
            nameof(SprintDuration), "Czas trwania sprintu");
        Check(problems, NumberOfSprints, MinNumberOfSprints, MaxNumberOfSprints,
            nameof(NumberOfSprints), "Liczba sprintów");
        Check(problems, BaselineIncrement, MinBaselineIncrement, MaxBaselineIncrement,
            nameof(BaselineIncrement), "Przyrost baseline");
        Check(problems, InitialBaseline, MinInitialBaseline, MaxInitialBaseline,
            nameof(InitialBaseline), "Początkowy baseline");
        Check(problems, FatigueDepth, MinFatigueDepth, MaxFatigueDepth,
            nameof(FatigueDepth), "Głębokość zmęczenia");
        Check(problems, SupercompensationPeak, MinSupercompensationPeak,
            MaxSupercompensationPeak, nameof(SupercompensationPeak),
            "Szczyt superkompensacji");

        return problems;
    }

    /// <summary>
    /// A NaN value fails every comparison, so `value < min || value > max` would let it
    /// through. The test is inverted for that reason: anything not inside the range,
    /// including NaN and the infinities, is a violation.
    /// </summary>
    private static void Check(
        List<string> problems,
        double value,
        double min,
        double max,
        string property,
        string label)
    {
        if (!(value >= min && value <= max))
        {
            problems.Add($"{label} ({property}) = {value}; dozwolony zakres to {min}–{max}.");
        }
    }
}

public class TeamMember
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public double Weight { get; set; } = 1.0;

    public static List<TeamMember> GetDefaultTeam() =>
    [
        new() { Name = "Dev 1", Role = "Developer", Weight = 1.0 },
        new() { Name = "Dev 2", Role = "Developer", Weight = 1.0 },
        new() { Name = "Dev 3", Role = "Developer", Weight = 1.0 },
        new() { Name = "Tester", Role = "QA", Weight = 0.9 },
        new() { Name = "DevOps", Role = "DevOps", Weight = 0.95 },
        new() { Name = "PO", Role = "Product Owner", Weight = 0.7 },
        new() { Name = "SM", Role = "Scrum Master", Weight = 0.8 },
    ];
}

public class SprintDataPoint
{
    public int Day { get; set; }
    public int SprintNumber { get; set; }
    public double DayInSprint { get; set; }
    public string Phase { get; set; } = string.Empty;
    public double Baseline { get; set; }
    public double Deviation { get; set; }
    public double Performance { get; set; }
    public double TeamWeight { get; set; }
}

public class SprintSummary
{
    public int SprintNumber { get; set; }
    public double Baseline { get; set; }
    public double PeakPerformance { get; set; }
    public double MinPerformance { get; set; }
    public double DeliveryValue { get; set; }
    public int DeliveryDay { get; set; }
}

public class ChartDataSet
{
    public List<double> Days { get; set; } = [];
    public List<double> Performance { get; set; } = [];
    public List<double> Baselines { get; set; } = [];
    public List<string> Phases { get; set; } = [];
    public List<SprintSummary> Summaries { get; set; } = [];
}
