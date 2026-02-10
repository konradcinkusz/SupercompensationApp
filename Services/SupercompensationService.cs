namespace SupercompensationApp.Services;

using SupercompensationApp.Models;

public class SupercompensationService
{
    /// <summary>
    /// Core fatigue/supercompensation curve function.
    /// Returns deviation from baseline for a given day within a sprint.
    ///
    /// Phases:
    ///   - Fatigue (0 → fatigueEnd):       0 → -FatigueDepth (smooth descent)
    ///   - Recovery (fatigueEnd → recovEnd): -FatigueDepth → 0 (return to baseline)
    ///   - Supercompensation (recovEnd → 1): 0 → +SupercompensationPeak (above baseline)
    /// </summary>
    public double CalculateDeviation(double normalizedDay, SprintConfiguration config)
    {
        var fatigueEnd = config.FatiguePhaseEnd;
        var recoveryEnd = config.RecoveryPhaseEnd;

        if (normalizedDay <= fatigueEnd)
        {
            // Fatigue phase: smooth quadratic descent
            var t = normalizedDay / fatigueEnd; // [0, 1]
            return -config.FatigueDepth * (t * t);
        }
        else if (normalizedDay <= recoveryEnd)
        {
            // Recovery phase: smooth quadratic ascent back to 0
            var t = (normalizedDay - fatigueEnd) / (recoveryEnd - fatigueEnd); // [0, 1]
            return -config.FatigueDepth * ((1.0 - t) * (1.0 - t));
        }
        else
        {
            // Supercompensation phase: sine rise above baseline
            var t = (normalizedDay - recoveryEnd) / (1.0 - recoveryEnd); // [0, 1]
            return config.SupercompensationPeak * Math.Sin(t * Math.PI / 2.0);
        }
    }

    public string GetPhase(double normalizedDay, SprintConfiguration config)
    {
        if (normalizedDay <= config.FatiguePhaseEnd) return "Fatigue";
        if (normalizedDay <= config.RecoveryPhaseEnd) return "Recovery";
        return "Supercompensation";
    }

    public double CalculateTeamWeight(List<TeamMember> team)
    {
        return team.Count > 0 ? team.Sum(t => t.Weight) : 1.0;
    }

    /// <summary>
    /// Generate full dataset: fine-grained points for smooth chart + discrete daily data.
    /// </summary>
    public ChartDataSet GenerateChartData(SprintConfiguration config, List<TeamMember> team, int pointsPerSprint = 50)
    {
        var teamWeight = CalculateTeamWeight(team);
        var totalDays = config.NumberOfSprints * config.SprintDuration;
        var totalPoints = config.NumberOfSprints * pointsPerSprint;

        var result = new ChartDataSet();

        // Fine-grained data for smooth chart
        for (int i = 0; i <= totalPoints; i++)
        {
            var day = (double)i / totalPoints * totalDays;
            var sprintNum = Math.Min((int)(day / config.SprintDuration), config.NumberOfSprints - 1);
            var dayInSprint = day - (sprintNum * config.SprintDuration);
            var normalizedDay = dayInSprint / config.SprintDuration;

            var baseline = config.InitialBaseline + sprintNum * config.BaselineIncrement;
            var deviation = CalculateDeviation(normalizedDay, config);
            var performance = (baseline + deviation) * teamWeight;
            var baselineScaled = baseline * teamWeight;
            var phase = GetPhase(normalizedDay, config);

            result.Days.Add(Math.Round(day, 2));
            result.Performance.Add(Math.Round(performance, 2));
            result.Baselines.Add(Math.Round(baselineScaled, 2));
            result.Phases.Add(phase);
        }

        // Sprint summaries
        for (int s = 0; s < config.NumberOfSprints; s++)
        {
            var baseline = config.InitialBaseline + s * config.BaselineIncrement;
            var baselineScaled = baseline * teamWeight;

            double peakPerf = double.MinValue;
            double minPerf = double.MaxValue;
            double deliveryValue = 0;
            int deliveryDay = (s + 1) * config.SprintDuration;

            // Calculate using numerical integration
            int steps = 200;
            for (int i = 0; i <= steps; i++)
            {
                var t = (double)i / steps;
                var dev = CalculateDeviation(t, config);
                var perf = (baseline + dev) * teamWeight;

                peakPerf = Math.Max(peakPerf, perf);
                minPerf = Math.Min(minPerf, perf);

                // Delivery value = integral of (performance - baseline) where performance > baseline
                if (dev > 0 && i > 0)
                {
                    var dt = config.SprintDuration / (double)steps;
                    deliveryValue += dev * teamWeight * dt;
                }
            }

            result.Summaries.Add(new SprintSummary
            {
                SprintNumber = s + 1,
                Baseline = Math.Round(baselineScaled, 2),
                PeakPerformance = Math.Round(peakPerf, 2),
                MinPerformance = Math.Round(minPerf, 2),
                DeliveryValue = Math.Round(deliveryValue, 2),
                DeliveryDay = deliveryDay
            });
        }

        return result;
    }

    /// <summary>
    /// Generate discrete daily data points for tabular display.
    /// </summary>
    public List<SprintDataPoint> GenerateTableData(SprintConfiguration config, List<TeamMember> team)
    {
        var teamWeight = CalculateTeamWeight(team);
        var points = new List<SprintDataPoint>();

        for (int day = 0; day < config.NumberOfSprints * config.SprintDuration; day++)
        {
            var sprintNum = day / config.SprintDuration;
            var dayInSprint = day % config.SprintDuration;
            var normalizedDay = (double)dayInSprint / config.SprintDuration;

            var baseline = config.InitialBaseline + sprintNum * config.BaselineIncrement;
            var deviation = CalculateDeviation(normalizedDay, config);

            points.Add(new SprintDataPoint
            {
                Day = day + 1,
                SprintNumber = sprintNum + 1,
                DayInSprint = dayInSprint + 1,
                Phase = GetPhase(normalizedDay, config),
                Baseline = Math.Round(baseline * teamWeight, 2),
                Deviation = Math.Round(deviation * teamWeight, 2),
                Performance = Math.Round((baseline + deviation) * teamWeight, 2),
                TeamWeight = teamWeight
            });
        }

        return points;
    }
}
