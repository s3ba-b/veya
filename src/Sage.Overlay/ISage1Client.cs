namespace Sage.Overlay;

/// <summary>
/// Thin client for <c>org.sage.Sage1</c> (docs/dbus-interfaces.md).
/// Abstracted so <see cref="OverlayViewModel"/> can be tested without a D-Bus
/// session bus (CLAUDE.md hard rule #3); <see cref="Sage1Client"/> is the real
/// implementation.
/// </summary>
public interface ISage1Client
{
    public Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default);
}
