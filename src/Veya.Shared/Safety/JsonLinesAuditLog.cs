using System.Text.Json;

namespace Veya.Shared.Safety;

/// <summary>
/// Writes <see cref="AuditEvent"/>s as JSON Lines to <c>audit.jsonl</c> in
/// <paramref name="directory"/>, rotating the file once it reaches
/// <paramref name="maxFileSizeBytes"/>.
/// </summary>
public sealed class JsonLinesAuditLog(string directory, long maxFileSizeBytes = 10 * 1024 * 1024) : IAuditLog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _filePath = Path.Combine(directory, "audit.jsonl");

    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        RotateIfNeeded();

        var line = JsonSerializer.Serialize(auditEvent, SerializerOptions);
        await File.AppendAllTextAsync(_filePath, line + "\n", cancellationToken);
    }

    private void RotateIfNeeded()
    {
        var info = new FileInfo(_filePath);
        if (info.Exists && info.Length >= maxFileSizeBytes)
        {
            var rotated = Path.Combine(directory, $"audit-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.jsonl");
            File.Move(_filePath, rotated);
        }
    }
}
