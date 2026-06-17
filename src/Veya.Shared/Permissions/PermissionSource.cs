namespace Veya.Shared.Permissions;

/// <summary>
/// A context source or action surface that the user grants access to
/// independently (docs/security.md, "Per-source permissions"). Defaults are
/// deny; each source is gated separately so nothing is read or written
/// "because it was convenient".
/// </summary>
public enum PermissionSource
{
    /// <summary>The system clipboard (first write target, Milestone 2).</summary>
    Clipboard,

    /// <summary>User files indexed or read on demand.</summary>
    Files,

    /// <summary>Desktop notifications.</summary>
    Notifications,

    /// <summary>Screen contents / screen awareness.</summary>
    Screen,

    /// <summary>The personal context index.</summary>
    PersonalIndex,

    /// <summary>The microphone, for voice input (ADR-0015).</summary>
    Microphone,
}
