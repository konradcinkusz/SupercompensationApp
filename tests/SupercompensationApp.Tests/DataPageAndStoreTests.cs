namespace SupercompensationApp.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using SupercompensationApp.Services;
using Xunit;

/// <summary>
/// The Data page's filters, and the real localStorage adapter.
///
/// StatePersistenceTests exercises InMemoryStateStore, which is a different class written
/// for those tests. LocalStorageStateStore — the one the application actually runs — had
/// no tests at all, and its behaviour is entirely about what it does when the interop
/// FAILS. That is deliberate and documented: reading window.localStorage throws outright
/// in a private window or when a browser is set to block site data, so the adapter turns
/// a JSException into a null and lets the application carry on without persistence.
///
/// It is also what made #45 hard to diagnose, because it makes a failed read and a first
/// visit indistinguishable to LoadAsync. Deliberate, documented and untested is one
/// refactor away from being none of the three.
/// </summary>
public class DataPageAndStoreTests : BunitContext
{
    private AppStateService NewGeneratedState()
    {
        var state = new AppStateService(new SupercompensationService(), new InMemoryStateStore());
        Services.AddSingleton(state);
        JSInterop.Mode = JSRuntimeMode.Loose;
        Assert.True(state.Generate());
        return state;
    }

    private static int BodyRows(IRenderedComponent<SupercompensationApp.Pages.Data> cut) =>
        cut.FindAll("tbody tr").Count;

    /// <summary>
    /// The two filters carry no class of their own and are told apart by order, so the
    /// count is asserted: a third filter would otherwise silently retarget these tests.
    /// </summary>
    private static AngleSharp.Dom.IElement Filter(
        IRenderedComponent<SupercompensationApp.Pages.Data> cut, int index)
    {
        var filters = cut.FindAll(".filter-row select");
        Assert.Equal(2, filters.Count);
        return filters[index];
    }

    [Fact]
    public void TheTableShowsEveryRowUntilSomethingIsFiltered()
    {
        var state = NewGeneratedState();
        var rows = state.LastTableData!;
        var cut = Render<SupercompensationApp.Pages.Data>();

        Assert.Equal(rows.Count, BodyRows(cut));
    }

    [Fact]
    public void FilteringBySprintKeepsOnlyThatSprint()
    {
        var state = NewGeneratedState();
        var rows = state.LastTableData!;
        var cut = Render<SupercompensationApp.Pages.Data>();

        Filter(cut, 0).Change("2");

        var expected = rows.Count(d => d.SprintNumber == 2);
        Assert.Equal(expected, BodyRows(cut));
        Assert.True(expected > 0 && expected < rows.Count,
            "the fixture has to have some rows in sprint 2 and some outside it, or this " +
            "assertion holds for a filter that does nothing");
    }

    [Fact]
    public void FilteringByPhaseKeepsOnlyThatPhase()
    {
        var state = NewGeneratedState();
        var rows = state.LastTableData!;
        var phase = rows.Select(d => d.Phase).First(p => !string.IsNullOrEmpty(p));
        var cut = Render<SupercompensationApp.Pages.Data>();

        Filter(cut, 1).Change(phase);

        var expected = rows.Count(d => d.Phase == phase);
        Assert.Equal(expected, BodyRows(cut));
        Assert.True(expected < rows.Count, "the phase filter must exclude something");
    }

    [Fact]
    public void TheTwoFiltersCompose()
    {
        var state = NewGeneratedState();
        var rows = state.LastTableData!;
        var phase = rows.Select(d => d.Phase).First(p => !string.IsNullOrEmpty(p));
        var cut = Render<SupercompensationApp.Pages.Data>();

        Filter(cut, 0).Change("2");
        Filter(cut, 1).Change(phase);

        var expected = rows.Count(d => d.SprintNumber == 2 && d.Phase == phase);
        Assert.Equal(expected, BodyRows(cut));

        // Composition is the point: it must be narrower than either filter alone, or the
        // second one is being dropped and this test would pass on a single filter.
        Assert.True(expected < rows.Count(d => d.SprintNumber == 2));
        Assert.True(expected < rows.Count(d => d.Phase == phase));
    }

    // ── The real adapter, which only has behaviour when the interop fails ────────

    /// <summary>An IJSRuntime that throws, which is what a private window produces.</summary>
    private sealed class ThrowingJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new JSException("localStorage is not available");

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new JSException("localStorage is not available");
    }

    [Fact]
    public async Task AReadThatThrowsIsReportedAsNothingStoredRatherThanCrashing()
    {
        var store = new LocalStorageStateStore(new ThrowingJsRuntime());

        Assert.Null(await store.ReadAsync(StateSerializer.StorageKey));
    }

    [Fact]
    public async Task AWriteThatThrowsIsSwallowed()
    {
        var store = new LocalStorageStateStore(new ThrowingJsRuntime());

        // Losing the write is strictly better than losing the session, so this must not
        // propagate — the application works without persistence.
        await store.WriteAsync(StateSerializer.StorageKey, "{}");
        await store.RemoveAsync(StateSerializer.StorageKey);
    }

    [Fact]
    public async Task AFailedRestoreIsNotReportedToTheUserAsCorruption()
    {
        var state = new AppStateService(
            new SupercompensationService(), new LocalStorageStateStore(new ThrowingJsRuntime()));

        await state.LoadAsync();

        // RestoreFailed means "a payload was found and could not be used". Storage being
        // unavailable is not that, and telling a private-window user their configuration
        // was corrupt would be wrong.
        Assert.False(state.RestoreFailed);
        Assert.Equal(10, state.Config.SprintDuration);
    }
}
