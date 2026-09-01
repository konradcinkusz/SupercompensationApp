namespace SupercompensationApp.Services;

using Microsoft.JSInterop;

/// <summary>
/// Browser localStorage, through the guarded helpers in wwwroot/js/storage-interop.js.
///
/// The guarding lives in JavaScript rather than here because that is where the throw
/// happens: reading window.localStorage raises a SecurityError outright in a private
/// window or when a browser is set to block site data, before any key is named. Catching
/// on the .NET side would work for the interop call but leaves the failure mode
/// documented in the wrong file.
/// </summary>
public class LocalStorageStateStore : IStateStore
{
    private readonly IJSRuntime _js;

    public LocalStorageStateStore(IJSRuntime js)
    {
        _js = js ?? throw new ArgumentNullException(nameof(js));
    }

    public async ValueTask<string?> ReadAsync(string key)
    {
        try
        {
            return await _js.InvokeAsync<string?>("supercompStorage.read", key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async ValueTask WriteAsync(string key, string value)
    {
        try
        {
            await _js.InvokeVoidAsync("supercompStorage.write", key, value);
        }
        catch (JSException)
        {
            // Storage is unavailable or full. The application works without it; losing
            // the write is strictly better than losing the session.
        }
    }

    public async ValueTask RemoveAsync(string key)
    {
        try
        {
            await _js.InvokeVoidAsync("supercompStorage.remove", key);
        }
        catch (JSException)
        {
        }
    }
}
