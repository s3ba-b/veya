using Tmds.DBus;
using Veya.Shared;
using Veya.Shared.Inference;

namespace Veya.Daemon;

/// <summary>
/// Implementation of org.veya.Veya1. <see cref="AskAsync"/> routes the prompt
/// through the <see cref="IModelRouter"/> (model router); real session/context
/// management arrives in later issues.
/// </summary>
public sealed class Veya1Service(IModelRouter modelRouter) : IVeya1
{
    public ObjectPath ObjectPath => new(VeyaDBus.ObjectPath);

    public async Task<string> AskAsync(string prompt)
    {
        try
        {
            return await modelRouter.AskAsync(prompt).ConfigureAwait(false);
        }
        catch (BackendUnavailableException ex)
        {
            return $"Veya can't reach its model backend right now: {ex.Message}";
        }
    }
}
