using Sage.Shared;
using Tmds.DBus;

namespace Sage.Daemon;

/// <summary>
/// Implementation of org.sage.Sage1. Milestone 1 stub: <see cref="AskAsync"/>
/// echoes the prompt back. Real session/context management and model routing
/// arrive in later issues.
/// </summary>
public sealed class Sage1Service : ISage1
{
    public ObjectPath ObjectPath => new(SageDBus.ObjectPath);

    public Task<string> AskAsync(string prompt) =>
        Task.FromResult($"Sage received: {prompt}");
}
