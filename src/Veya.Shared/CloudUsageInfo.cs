using System.Runtime.InteropServices;

namespace Veya.Shared;

/// <summary>
/// Payload of the <c>CloudUsage</c> D-Bus signal (docs/dbus-interfaces.md): the
/// user-visible record that a request left the machine to a cloud backend. Mirrors
/// the <c>cloud.request</c> audit event — backend, model, and token counts only,
/// never prompt or response content. Marshaled as a D-Bus struct <c>(ssuu)</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CloudUsageInfo
{
    /// <summary>The cloud backend that served the request: <c>"mistral"</c> or <c>"claude"</c>.</summary>
    public string Backend;

    /// <summary>The model name reported by that backend.</summary>
    public string Model;

    /// <summary>Input (prompt) token count for the call.</summary>
    public uint InputTokens;

    /// <summary>Output (completion) token count for the call.</summary>
    public uint OutputTokens;
}
