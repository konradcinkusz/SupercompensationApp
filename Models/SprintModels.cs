namespace SupercompensationApp.Models;

public class SprintConfiguration
{
    public int SprintDuration { get; set; } = 10;
    public int NumberOfSprints { get; set; } = 3;
    public double BaselineIncrement { get; set; } = 15.0;
    public double InitialBaseline { get; set; } = 100.0;
    public double FatigueDepth { get; set; } = 25.0;
    public double SupercompensationPeak { get; set; } = 18.0;

    // Phases as fractions of sprint duration
    public double FatiguePhaseEnd => 0.5;   // 50% of sprint
    public double RecoveryPhaseEnd => 0.8;  // 80% of sprint
    // Remaining 20% = supercompensation
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
