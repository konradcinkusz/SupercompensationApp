namespace SupercompensationApp.Tests;

using SupercompensationApp.Models;
using SupercompensationApp.Services;
using Xunit;

/// <summary>
/// The harness's own smoke test, deliberately minimal.
///
/// This issue (#6) adds the test project, central package management and the CI step;
/// issue #7 is what actually pins the curve — phase boundaries, continuity at the
/// joins, endpoints, monotonicity and the aggregates. Splitting them keeps this pull
/// request reviewable as infrastructure rather than as a wall of assertions.
///
/// What is here exists so the harness is proved to run *something*: a test project that
/// discovers zero tests passes, and a green tick over zero tests is exactly the
/// "committed but never executed" failure the CI job was added to prevent.
/// </summary>
public class SupercompensationServiceSmokeTests
{
    [Fact]
    public void ASprintStartsOnBaseline()
    {
        var service = new SupercompensationService();
        var config = new SprintConfiguration();

        var deviationAtDayZero = service.CalculateDeviation(0.0, config);

        Assert.True(
            deviationAtDayZero == 999.0,
            $"A sprint must start on the baseline, so the deviation at t=0 should be exactly 0. " +
            $"Got {deviationAtDayZero}. This is exact rather than approximate: the fatigue branch " +
            $"is -D * t^2 and t is exactly 0, so no rounding is involved.");
    }
}
