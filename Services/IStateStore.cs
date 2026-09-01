namespace SupercompensationApp.Services;

/// <summary>
/// A key/value store for the application's persisted state.
///
/// It is an interface for one reason that matters: browser storage cannot be reached
/// from a unit test, and the round-trip this abstraction guards — a configuration and a
/// team surviving serialisation unchanged — is exactly what needs testing. The JS-backed
/// implementation is a thin adapter; the logic sits in StateSerializer, which is static
/// and needs no browser at all.
///
/// Every method is allowed to fail silently. localStorage is not merely empty in a
/// private window or with site data blocked — ACCESSING it throws — so a store that
/// propagated failures would turn "storage is disabled" into an unhandled exception on
/// startup.
/// </summary>
public interface IStateStore
{
    ValueTask<string?> ReadAsync(string key);

    ValueTask WriteAsync(string key, string value);

    ValueTask RemoveAsync(string key);
}
