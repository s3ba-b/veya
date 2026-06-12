using Sage.Shared;
using Tmds.DBus;

namespace Sage.Overlay;

/// <summary>
/// <see cref="ISage1Client"/> backed by a real D-Bus session bus connection
/// to <c>org.sage.Sage1</c>. The connection is established lazily on first
/// use and kept open for reuse.
/// </summary>
public sealed class Sage1Client : ISage1Client, IAsyncDisposable
{
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private Connection? _connection;
    private ISage1? _proxy;

    public async Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var proxy = await GetProxyAsync(cancellationToken).ConfigureAwait(false);
        return await proxy.AskAsync(prompt).ConfigureAwait(false);
    }

    private async Task<ISage1> GetProxyAsync(CancellationToken cancellationToken)
    {
        if (_proxy is not null)
        {
            return _proxy;
        }

        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_proxy is not null)
            {
                return _proxy;
            }

            var address = Address.Session;
            if (string.IsNullOrEmpty(address))
            {
                throw new InvalidOperationException("No D-Bus session bus is available.");
            }

            var connection = new Connection(address);
            await connection.ConnectAsync().ConfigureAwait(false);
            _connection = connection;
            _proxy = connection.CreateProxy<ISage1>(SageDBus.BusName, new ObjectPath(SageDBus.ObjectPath));
            return _proxy;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        _connection?.Dispose();
        return ValueTask.CompletedTask;
    }
}
