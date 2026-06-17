using System.Diagnostics;
using Veya.Shared.Safety;

namespace Veya.Shared.Inference;

/// <summary>
/// An <see cref="IInferenceBackend"/> decorator that writes one audit event per
/// successful completion, keeping the wrapped backends focused solely on their
/// provider protocol (SRP). This is the single source of truth for the inference
/// audit path (docs/security.md): a <c>cloud.request</c> event when the backend
/// sends data off the machine, or a <c>local.request</c> event when it does not.
/// Events deliberately carry no prompt or response content — only the backend,
/// model, token counts, and duration.
/// </summary>
/// <remarks>
/// Audit is written only on success: if <paramref name="inner"/> throws (e.g.
/// <see cref="BackendUnavailableException"/>), no event is recorded, matching the
/// pre-decorator behaviour where backends logged after a successful call.
/// </remarks>
public sealed class AuditingInferenceBackend(IInferenceBackend inner, IAuditLog auditLog, string backendName, string model, bool isLocal)
    : IInferenceBackend
{
    public async Task<InferenceResponse> CompleteAsync(InferenceRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await inner.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        var auditEvent = isLocal
            ? AuditEvent.LocalRequest(backendName, model, response.InputTokens, response.OutputTokens, stopwatch.Elapsed)
            : AuditEvent.CloudRequest(backendName, model, response.InputTokens, response.OutputTokens, stopwatch.Elapsed);
        await auditLog.WriteAsync(auditEvent, cancellationToken).ConfigureAwait(false);

        return response;
    }
}
