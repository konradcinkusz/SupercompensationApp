namespace SupercompensationApp.Tests;

using SupercompensationApp.Models;
using SupercompensationApp.Services;
using Xunit;

/// <summary>
/// Pins the supercompensation curve.
///
/// The README states the model as a table of three formulas; nothing checked that the
/// code still computes that table. These are the properties a careless edit breaks, and
/// none of them can be seen by looking at a rendered chart: the branches are three
/// separate arithmetic expressions and the joins between them are implicit, so a change
/// to any one can break a join without breaking a build.
///
/// Note what this is NOT. CalculateDeviation is continuous as written today — these
/// tests are not a bug report, they are what stops that becoming false silently.
///
/// Every property is asserted at two configurations: the defaults, and a second set
/// chosen so that no assertion can accidentally depend on D = 25, P = 18, a 10-day
/// sprint or a 7-person team.
/// </summary>
public class SupercompensationCurveTests
{
    private const double FatigueEnd = 0.5;
    private const double RecoveryEnd = 0.8;

    /// <summary>The two configurations every property below is checked against.</summary>
    private static (string Name, SprintConfiguration Config)[] Configurations() =>
    [
        ("defaults", new SprintConfiguration()),
        ("non-default", new SprintConfiguration
        {
            SprintDuration = 7,
            NumberOfSprints = 5,
            InitialBaseline = 120.0,
            BaselineIncrement = 8.5,
            FatigueDepth = 40.0,
            SupercompensationPeak = 12.0,
        }),
    ];

    private static List<TeamMember> SmallTeam() =>
    [
        new() { Name = "A", Role = "Developer", Weight = 1.0 },
        new() { Name = "B", Role = "QA", Weight = 0.5 },
    ];

    // ── Phase classification ─────────────────────────────────────────────────
    //
    // The comparisons in GetPhase are `<=`, so 0.5 is Fatigue and 0.8 is Recovery.
    // Which side of a boundary falls into which phase is a decision; it belongs in a
    // test rather than being rediscovered from the source every time somebody asks.

    [Fact]
    public void PhaseBoundariesFallOnTheLowerPhase()
    {
        var service = new SupercompensationService();

        foreach (var (name, config) in Configurations())
        {
            AssertPhase(service, config, name, 0.0, "Fatigue");
            AssertPhase(service, config, name, 0.25, "Fatigue");
            AssertPhase(service, config, name, FatigueEnd, "Fatigue");

            AssertPhase(service, config, name, 0.5000001, "Recovery");
            AssertPhase(service, config, name, 0.65, "Recovery");
            AssertPhase(service, config, name, RecoveryEnd, "Recovery");

            AssertPhase(service, config, name, 0.8000001, "Supercompensation");
            AssertPhase(service, config, name, 1.0, "Supercompensation");
        }
    }

    private static void AssertPhase(
        SupercompensationService service,
        SprintConfiguration config,
        string configName,
        double t,
        string expected)
    {
        var actual = service.GetPhase(t, config);
        Assert.True(
            actual == expected,
            $"[{configName}] The phase at t={t} should be {expected} but was {actual}. " +
            $"The boundaries are inclusive on the lower phase: 0.5 is the last Fatigue " +
            $"point and 0.8 is the last Recovery point.");
    }

    // ── Endpoints ────────────────────────────────────────────────────────────
    //
    // The first three are EXACT and the fourth is not, and the difference is not
    // pedantry. f(0), f(0.5) and f(0.8) fall out of multiplication and subtraction on
    // values that are their own quotients, so no rounding enters. f(1) goes through
    // Math.Sin, whose result is a property of the runtime's libm rather than of this
    // model — so it gets a bound. Asserting a remembered figure there would be
    // committing an observation where an invariant was meant.

    [Fact]
    public void ASprintStartsExactlyOnTheBaseline()
    {
        var service = new SupercompensationService();

        foreach (var (name, config) in Configurations())
        {
            var value = service.CalculateDeviation(0.0, config);
            Assert.True(
                value == 0.0,
                $"[{name}] A sprint must start on the baseline, so the deviation at t=0 " +
                $"is exactly 0. Got {value}.");
        }
    }

    [Fact]
    public void TheTroughIsExactlyTheConfiguredFatigueDepth()
    {
        var service = new SupercompensationService();

        foreach (var (name, config) in Configurations())
        {
            var value = service.CalculateDeviation(FatigueEnd, config);
            Assert.True(
                value == -config.FatigueDepth,
                $"[{name}] The fatigue phase must bottom out at exactly the configured " +
                $"depth, -{config.FatigueDepth}. Got {value}. If this drifts, the number " +
                $"the user typed is not the number the chart shows.");
        }
    }

    [Fact]
    public void RecoveryReturnsExactlyToTheBaseline()
    {
        var service = new SupercompensationService();

        foreach (var (name, config) in Configurations())
        {
            var value = service.CalculateDeviation(RecoveryEnd, config);
            Assert.True(
                value == 0.0,
                $"[{name}] Recovery must return exactly to the baseline at t=0.8 — that is " +
                $"what makes the phase boundary meaningful. Got {value}.");
        }
    }

    [Fact]
    public void TheSprintEndsAtTheConfiguredSupercompensationPeak()
    {
        var service = new SupercompensationService();

        // The model's own arithmetic is exact here; the sine is not. One ulp at the
        // magnitude of the peak is the honest ceiling, and it holds on any libm.
        foreach (var (name, config) in Configurations())
        {
            var value = service.CalculateDeviation(1.0, config);
            var slack = Math.Abs(value - config.SupercompensationPeak);

            // Note what is NOT used here: C#'s double.Epsilon is the smallest denormal
            // (about 5e-324), not machine epsilon. A tolerance written in terms of it
            // reads like a relative ulp bound and is effectively zero.
            var ceiling = 1e-12 * Math.Max(1.0, Math.Abs(config.SupercompensationPeak));

            Assert.True(
                slack <= ceiling,
                $"[{name}] A sprint must end at the configured supercompensation peak, " +
                $"{config.SupercompensationPeak}. Got {value}, off by {slack}, which is " +
                $"more than rounding in Math.Sin can account for.");
        }
    }

    // ── Continuity at the joins ──────────────────────────────────────────────

    [Fact]
    public void TheCurveIsContinuousAtBothJoins()
    {
        var service = new SupercompensationService();
        const double Step = 1e-6;

        foreach (var (name, config) in Configurations())
        {
            foreach (var join in new[] { FatigueEnd, RecoveryEnd })
            {
                var below = service.CalculateDeviation(join - Step, config);
                var above = service.CalculateDeviation(join + Step, config);
                var jump = Math.Abs(above - below);

                // The bound is DERIVED rather than measured, so it holds on any runner.
                // A remembered residual would be a property of the machine that measured
                // it. Either side moves at most (slope x Step) away from the join, so
                // what is needed is the steepest slope on the curve, in normalised-day
                // units:
                //
                //   fatigue           d/dx [-D (x/0.5)^2]        peaks at 4 D
                //   recovery          d/dx [-D (1-u)^2],  u=(x-0.5)/0.3   peaks at 2D/0.3
                //   supercompensation d/dx [P sin(u pi/2)], u=(x-0.8)/0.2 peaks at (pi/2)P/0.2
                //
                // The last two are about 6.7 D and 7.9 P — steeper than the first, which
                // is the mistake in an earlier draft of this test. 8 x max(D, P) covers
                // all three with room to spare.
                var slopeBound = 8.0 * Math.Max(config.FatigueDepth, config.SupercompensationPeak);
                var ceiling = 3.0 * slopeBound * Step;

                Assert.True(
                    jump <= ceiling,
                    $"[{name}] The curve must not step at the join t={join}: the value " +
                    $"just below is {below} and just above is {above}, a jump of {jump}. " +
                    $"Two adjacent branches disagreeing at a boundary is a discontinuity " +
                    $"the chart will draw as a vertical line.");
            }
        }
    }

    // ── Monotonicity ─────────────────────────────────────────────────────────
    //
    // This is the assertion that catches a sign error in one branch. A flipped sign
    // still produces a smooth, plausible curve; it just runs the wrong way.

    [Fact]
    public void TheCurveFallsThroughFatigueAndRisesAllTheWayBack()
    {
        var service = new SupercompensationService();
        const int Samples = 200;

        foreach (var (name, config) in Configurations())
        {
            AssertStrictlyMonotonic(
                service, config, name, from: 0.0, to: FatigueEnd, Samples, rising: false);

            AssertStrictlyMonotonic(
                service, config, name, from: FatigueEnd, to: 1.0, Samples, rising: true);
        }
    }

    private static void AssertStrictlyMonotonic(
        SupercompensationService service,
        SprintConfiguration config,
        string configName,
        double from,
        double to,
        int samples,
        bool rising)
    {
        var previous = service.CalculateDeviation(from, config);

        for (var i = 1; i <= samples; i++)
        {
            var t = from + ((to - from) * i / samples);
            var current = service.CalculateDeviation(t, config);
            var ordered = rising ? current > previous : current < previous;

            Assert.True(
                ordered,
                $"[{configName}] The curve must be strictly " +
                $"{(rising ? "increasing" : "decreasing")} across " +
                $"[{from}, {to}], but at t={t} it went from {previous} to {current}. " +
                $"A sign error in one branch produces a curve that is still smooth and " +
                $"still plausible, and runs the wrong way.");

            previous = current;
        }
    }

    // ── Aggregates ───────────────────────────────────────────────────────────

    [Fact]
    public void EverySprintGetsASummaryAtItsOwnBaseline()
    {
        var service = new SupercompensationService();
        var team = SmallTeam();
        var teamWeight = service.CalculateTeamWeight(team);

        foreach (var (name, config) in Configurations())
        {
            var data = service.GenerateChartData(config, team);

            Assert.True(
                data.Summaries.Count == config.NumberOfSprints,
                $"[{name}] There must be one summary per sprint: expected " +
                $"{config.NumberOfSprints}, got {data.Summaries.Count}.");

            for (var s = 0; s < config.NumberOfSprints; s++)
            {
                var summary = data.Summaries[s];
                var expected = Math.Round(
                    (config.InitialBaseline + (s * config.BaselineIncrement)) * teamWeight, 2);

                Assert.True(
                    summary.Baseline == expected,
                    $"[{name}] Sprint {s + 1}'s baseline must be the progressive baseline " +
                    $"scaled by the team weight: expected {expected}, got {summary.Baseline}.");

                Assert.True(
                    summary.PeakPerformance > summary.Baseline,
                    $"[{name}] Sprint {s + 1} must peak above its baseline (the peak is " +
                    $"what supercompensation means): baseline {summary.Baseline}, peak " +
                    $"{summary.PeakPerformance}.");

                Assert.True(
                    summary.MinPerformance < summary.Baseline,
                    $"[{name}] Sprint {s + 1} must dip below its baseline during fatigue: " +
                    $"baseline {summary.Baseline}, min {summary.MinPerformance}.");

                Assert.True(
                    summary.DeliveryValue > 0.0,
                    $"[{name}] Sprint {s + 1}'s delivery value is the area above the " +
                    $"baseline and must be positive; got {summary.DeliveryValue}.");
            }
        }
    }

    [Fact]
    public void DeliveryValueScalesWithSprintLengthBecauseItIsAnIntegral()
    {
        // DeliveryValue is an accumulation over sprint days, not a rate. It sits on the
        // summary card beside peak and min, which ARE rates — so the fact that only this
        // one moves when the sprint gets longer is the property worth pinning.
        var service = new SupercompensationService();
        var team = SmallTeam();

        var shortSprint = new SprintConfiguration { SprintDuration = 10, NumberOfSprints = 1 };
        var longSprint = new SprintConfiguration { SprintDuration = 20, NumberOfSprints = 1 };

        var shortValue = service.GenerateChartData(shortSprint, team).Summaries[0].DeliveryValue;
        var longValue = service.GenerateChartData(longSprint, team).Summaries[0].DeliveryValue;

        // Exact in the arithmetic — doubling the duration doubles dt and nothing else —
        // so the only slack needed is the rounding to two decimal places that
        // GenerateChartData applies to both figures.
        var difference = Math.Abs(longValue - (2.0 * shortValue));

        Assert.True(
            difference <= 0.02,
            $"Doubling the sprint length must double the delivery value, because it is " +
            $"an integral over sprint days: 10 days gave {shortValue}, 20 days gave " +
            $"{longValue}, and twice the first is {2.0 * shortValue}.");
    }

    // ── Table ────────────────────────────────────────────────────────────────

    [Fact]
    public void TheTableIsOneRowPerDayWithConsistentOneBasedIndices()
    {
        var service = new SupercompensationService();
        var team = SmallTeam();

        foreach (var (name, config) in Configurations())
        {
            var rows = service.GenerateTableData(config, team);
            var expectedRows = config.NumberOfSprints * config.SprintDuration;

            Assert.True(
                rows.Count == expectedRows,
                $"[{name}] The table must have one row per day: expected {expectedRows} " +
                $"({config.NumberOfSprints} sprints x {config.SprintDuration} days), got " +
                $"{rows.Count}.");

            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];

                Assert.True(
                    row.Day == i + 1,
                    $"[{name}] Days must run 1..{expectedRows} with no gaps; row {i} " +
                    $"reports day {row.Day}.");

                var expectedSprint = (i / config.SprintDuration) + 1;
                var expectedDayInSprint = (i % config.SprintDuration) + 1;

                Assert.True(
                    row.SprintNumber == expectedSprint,
                    $"[{name}] Day {row.Day} belongs to sprint {expectedSprint}, but the " +
                    $"row says {row.SprintNumber}. Every Quiz route and summary reference " +
                    $"in the UI navigates by these numbers.");

                Assert.True(
                    row.DayInSprint == expectedDayInSprint,
                    $"[{name}] Day {row.Day} is day {expectedDayInSprint} of its sprint, " +
                    $"but the row says {row.DayInSprint}.");
            }
        }
    }
}
