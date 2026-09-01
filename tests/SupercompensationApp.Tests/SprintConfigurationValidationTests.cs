namespace SupercompensationApp.Tests;

using SupercompensationApp.Models;
using SupercompensationApp.Services;
using Xunit;

/// <summary>
/// The Konfiguracja tab guards its parameters in the markup only — `min` and `max` on an
/// &lt;input&gt; are validation HINTS. They do not stop a value being typed or pasted,
/// and @bind performs no range check of its own.
///
/// With SprintDuration = 0, `dayInSprint / config.SprintDuration` is 0/0. Every
/// deviation becomes NaN; every comparison against NaN is false, so CalculateDeviation
/// falls through to its final branch and the summaries keep the double.MinValue and
/// double.MaxValue they were initialised with. The chart renders blank, with no error
/// anywhere. That silence is what these tests exist to remove.
/// </summary>
public class SprintConfigurationValidationTests
{
    private static List<TeamMember> Team() =>
    [
        new() { Name = "A", Role = "Developer", Weight = 1.0 },
    ];

    // ── The reported defect ──────────────────────────────────────────────────

    [Fact]
    public void AZeroSprintDurationCannotReachTheCurve()
    {
        var service = new SupercompensationService();
        var config = new SprintConfiguration { SprintDuration = 0 };

        var chart = Assert.Throws<ArgumentOutOfRangeException>(
            () => { service.GenerateChartData(config, Team()); });
        var table = Assert.Throws<ArgumentOutOfRangeException>(
            () => { service.GenerateTableData(config, Team()); });

        foreach (var thrown in new[] { chart, table })
        {
            Assert.True(
                thrown.Message.Contains(nameof(SprintConfiguration.SprintDuration), StringComparison.Ordinal),
                $"The failure must name the offending property so the caller can act on " +
                $"it. Got: {thrown.Message}");
        }
    }

    [Fact]
    public void AZeroSprintCountIsRefusedToo()
    {
        var service = new SupercompensationService();
        var config = new SprintConfiguration { NumberOfSprints = 0 };

        var thrown = Assert.Throws<ArgumentOutOfRangeException>(
            () => { service.GenerateChartData(config, Team()); });

        Assert.True(
            thrown.Message.Contains(nameof(SprintConfiguration.NumberOfSprints), StringComparison.Ordinal),
            $"Got: {thrown.Message}");
    }

    [Fact]
    public void NegativeValuesAreRefused()
    {
        // A negative sprint count produced a negative sprint index through
        // Math.Min((int)(day / duration), N - 1), and with it a baseline BELOW the
        // configured initial one.
        var service = new SupercompensationService();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                service.GenerateChartData(
                new SprintConfiguration { SprintDuration = -10 }, Team());
            });

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
            {
                service.GenerateChartData(
                new SprintConfiguration { NumberOfSprints = -3 }, Team());
            });
    }

    // ── Every parameter, both directions ─────────────────────────────────────

    [Fact]
    public void EveryParameterIsRejectedOnBothSidesOfItsRange()
    {
        var cases = new (string Property, SprintConfiguration Config)[]
        {
            (nameof(SprintConfiguration.SprintDuration),
                new SprintConfiguration { SprintDuration = SprintConfiguration.MinSprintDuration - 1 }),
            (nameof(SprintConfiguration.SprintDuration),
                new SprintConfiguration { SprintDuration = SprintConfiguration.MaxSprintDuration + 1 }),
            (nameof(SprintConfiguration.NumberOfSprints),
                new SprintConfiguration { NumberOfSprints = SprintConfiguration.MinNumberOfSprints - 1 }),
            (nameof(SprintConfiguration.NumberOfSprints),
                new SprintConfiguration { NumberOfSprints = SprintConfiguration.MaxNumberOfSprints + 1 }),
            (nameof(SprintConfiguration.BaselineIncrement),
                new SprintConfiguration { BaselineIncrement = SprintConfiguration.MinBaselineIncrement - 1 }),
            (nameof(SprintConfiguration.BaselineIncrement),
                new SprintConfiguration { BaselineIncrement = SprintConfiguration.MaxBaselineIncrement + 1 }),
            (nameof(SprintConfiguration.InitialBaseline),
                new SprintConfiguration { InitialBaseline = SprintConfiguration.MinInitialBaseline - 1 }),
            (nameof(SprintConfiguration.InitialBaseline),
                new SprintConfiguration { InitialBaseline = SprintConfiguration.MaxInitialBaseline + 1 }),
            (nameof(SprintConfiguration.FatigueDepth),
                new SprintConfiguration { FatigueDepth = SprintConfiguration.MinFatigueDepth - 1 }),
            (nameof(SprintConfiguration.FatigueDepth),
                new SprintConfiguration { FatigueDepth = SprintConfiguration.MaxFatigueDepth + 1 }),
            (nameof(SprintConfiguration.SupercompensationPeak),
                new SprintConfiguration { SupercompensationPeak = SprintConfiguration.MinSupercompensationPeak - 1 }),
            (nameof(SprintConfiguration.SupercompensationPeak),
                new SprintConfiguration { SupercompensationPeak = SprintConfiguration.MaxSupercompensationPeak + 1 }),
        };

        foreach (var (property, config) in cases)
        {
            var problems = config.Validate();

            Assert.True(
                problems.Count == 1,
                $"{property} out of range should report exactly one problem, got " +
                $"{problems.Count}: {string.Join(" | ", problems)}");

            Assert.True(
                problems[0].Contains(property, StringComparison.Ordinal),
                $"The message must name {property}. Got: {problems[0]}");
        }
    }

    [Fact]
    public void TheBoundaryValuesThemselvesAreAccepted()
    {
        // An off-by-one in a validator is as much a defect as no validator: the ranges
        // are inclusive, and a reader who types the maximum the UI advertises must not
        // be told it is out of range.
        var boundaries = new[]
        {
            new SprintConfiguration
            {
                SprintDuration = SprintConfiguration.MinSprintDuration,
                NumberOfSprints = SprintConfiguration.MinNumberOfSprints,
                BaselineIncrement = SprintConfiguration.MinBaselineIncrement,
                InitialBaseline = SprintConfiguration.MinInitialBaseline,
                FatigueDepth = SprintConfiguration.MinFatigueDepth,
                SupercompensationPeak = SprintConfiguration.MinSupercompensationPeak,
            },
            new SprintConfiguration
            {
                SprintDuration = SprintConfiguration.MaxSprintDuration,
                NumberOfSprints = SprintConfiguration.MaxNumberOfSprints,
                BaselineIncrement = SprintConfiguration.MaxBaselineIncrement,
                InitialBaseline = SprintConfiguration.MaxInitialBaseline,
                FatigueDepth = SprintConfiguration.MaxFatigueDepth,
                SupercompensationPeak = SprintConfiguration.MaxSupercompensationPeak,
            },
        };

        foreach (var config in boundaries)
        {
            var problems = config.Validate();
            Assert.True(
                problems.Count == 0,
                $"Every range is inclusive, so its endpoints must be accepted. Got: " +
                $"{string.Join(" | ", problems)}");
        }
    }

    [Fact]
    public void NaNAndInfinityAreRefusedRatherThanSlippingThrough()
    {
        // Worth its own test because it is the trap in writing the check. A NaN fails
        // EVERY comparison, so `value < min || value > max` is false for NaN and would
        // wave it through into exactly the arithmetic this validation exists to prevent.
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var config = new SprintConfiguration { FatigueDepth = bad };
            var problems = config.Validate();

            Assert.True(
                problems.Count == 1,
                $"FatigueDepth = {bad} must be refused; the range test has to be written " +
                $"as 'not inside' rather than 'outside', because NaN fails both " +
                $"comparisons. Got {problems.Count} problems.");
        }
    }

    // ── Nothing inside the accepted range produces NaN ────────────────────────

    [Fact]
    public void NoAcceptedConfigurationCanProduceANonFiniteNumber()
    {
        // The point of the whole issue: for anything the validator lets through, every
        // number the chart and the table carry is finite.
        var service = new SupercompensationService();
        var team = Team();

        foreach (var duration in new[] { 5, 7, 10, 13, 30 })
        {
            foreach (var sprints in new[] { 1, 2, 5, 20 })
            {
                var config = new SprintConfiguration
                {
                    SprintDuration = duration,
                    NumberOfSprints = sprints,
                };

                Assert.True(config.Validate().Count == 0, "Sweep configuration must be valid.");

                var chart = service.GenerateChartData(config, team);
                var table = service.GenerateTableData(config, team);

                var values = chart.Performance
                    .Concat(chart.Baselines)
                    .Concat(chart.Days)
                    .Concat(chart.Summaries.SelectMany(s => new[]
                    {
                        s.Baseline, s.PeakPerformance, s.MinPerformance, s.DeliveryValue,
                    }))
                    .Concat(table.SelectMany(t => new[]
                    {
                        t.Baseline, t.Deviation, t.Performance,
                    }));

                foreach (var value in values)
                {
                    Assert.True(
                        double.IsFinite(value),
                        $"{duration}-day x {sprints} sprints produced {value}, which is " +
                        $"not finite. An accepted configuration must never yield NaN or " +
                        $"an infinity — that is what the validation is for.");
                }
            }
        }
    }
}
