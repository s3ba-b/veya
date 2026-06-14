namespace Veya.Overlay;

/// <summary>
/// Drives the overlay's single interaction: send a prompt to
/// <c>org.veya.Veya1</c> and report back the reply, or a friendly message if
/// the daemon can't be reached. Kept GTK-free so it can be tested without a
/// desktop session (ADR-0002).
/// </summary>
public sealed class OverlayViewModel(IVeya1Client client)
{
    public async Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return string.Empty;
        }

        try
        {
            return await client.AskAsync(prompt, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"Veya is unreachable: {ex.Message}";
        }
    }
}
