namespace Sage.Shared.Inference;

/// <summary>
/// Thrown when an <see cref="IInferenceBackend"/> cannot be used, e.g. because
/// no API key is configured. The Daemon maps this to
/// <c>org.sage.Sage1.Error.BackendUnavailable</c> (docs/dbus-interfaces.md).
/// </summary>
public sealed class BackendUnavailableException : Exception
{
    public BackendUnavailableException(string message)
        : base(message)
    {
    }

    public BackendUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
