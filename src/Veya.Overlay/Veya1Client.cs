using Tmds.DBus;
using Veya.Shared;

namespace Veya.Overlay;

/// <summary>
/// <see cref="IVeya1Client"/> backed by a real D-Bus session bus connection
/// to <c>org.veya.Veya1</c>. The connection is established lazily on first
/// use and kept open for reuse.
/// </summary>
public sealed class Veya1Client : IVeya1Client, IAsyncDisposable
{
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private Connection? _connection;
    private IVeya1? _proxy;

    public async Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var proxy = await GetProxyAsync(cancellationToken).ConfigureAwait(false);
        return await proxy.AskAsync(prompt).ConfigureAwait(false);
    }

    private async Task<IVeya1> GetProxyAsync(CancellationToken cancellationToken)
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
            _proxy = connection.CreateProxy<IVeya1>(VeyaDBus.BusName, new ObjectPath(VeyaDBus.ObjectPath));
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
