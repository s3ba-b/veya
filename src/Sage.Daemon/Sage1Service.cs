using Sage.Shared;
using Sage.Shared.Inference;
using Tmds.DBus;

namespace Sage.Daemon;

/// <summary>
/// Implementation of org.sage.Sage1. <see cref="AskAsync"/> routes the prompt
/// through the <see cref="IModelRouter"/> (model router); real session/context
/// management arrives in later issues.
/// </summary>
public sealed class Sage1Service(IModelRouter modelRouter) : ISage1
{
    public ObjectPath ObjectPath => new(SageDBus.ObjectPath);

    public async Task<string> AskAsync(string prompt)
    {
        try
        {
            return await modelRouter.AskAsync(prompt).ConfigureAwait(false);
        }
        catch (BackendUnavailableException ex)
        {
            return $"Sage can't reach its model backend right now: {ex.Message}";
        }
    }
}
