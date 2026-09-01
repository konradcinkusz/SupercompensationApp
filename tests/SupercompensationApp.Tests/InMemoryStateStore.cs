namespace SupercompensationApp.Tests;

using SupercompensationApp.Services;

/// <summary>
/// A test double for IStateStore. Browser storage cannot be reached from a unit test,
/// which is the whole reason IStateStore is an interface.
///
/// It can also be told to behave like storage that is unavailable — which is not a
/// hypothetical: reading window.localStorage throws outright in a private window and in
/// any browser set to block site data.
/// </summary>
public class InMemoryStateStore : IStateStore
{
    private readonly Dictionary<string, string> _items = new(StringComparer.Ordinal);

    /// <summary>Simulates storage that is present but refuses every operation.</summary>
    public bool Unavailable { get; set; }

    public int Writes { get; private set; }

    public int Removes { get; private set; }

    public string? Peek(string key) => _items.TryGetValue(key, out var v) ? v : null;

    public void Seed(string key, string value) => _items[key] = value;

    public ValueTask<string?> ReadAsync(string key) =>
        ValueTask.FromResult(Unavailable ? null : Peek(key));

    public ValueTask WriteAsync(string key, string value)
    {
        Writes++;
        if (!Unavailable)
        {
            _items[key] = value;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string key)
    {
        Removes++;
        _items.Remove(key);
        return ValueTask.CompletedTask;
    }
}
