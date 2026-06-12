using System.Text.Json;
using Sage.Shared.Safety;
using Xunit;

namespace Sage.Shared.Tests.Safety;

public class JsonLinesAuditLogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "sage-audit-tests-" + Guid.NewGuid());

    [Fact]
    public async Task WriteAsync_AppendsJsonLineWithExpectedShape()
    {
        var auditLog = new JsonLinesAuditLog(_directory);

        await auditLog.WriteAsync(AuditEvent.ToolExecDenied("test_tool", "rm", ["-rf", "/"]));

        var lines = await File.ReadAllLinesAsync(Path.Combine(_directory, "audit.jsonl"));
        var line = Assert.Single(lines);

        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;
        Assert.Equal("tool.exec", root.GetProperty("eventType").GetString());

        var fields = root.GetProperty("fields");
        Assert.Equal("test_tool", fields.GetProperty("tool").GetString());
        Assert.Equal("rm", fields.GetProperty("binary").GetString());
        Assert.False(fields.GetProperty("allowed").GetBoolean());
    }

    [Fact]
    public async Task WriteAsync_RotatesFileOnceItExceedsMaxSize()
    {
        var auditLog = new JsonLinesAuditLog(_directory, maxFileSizeBytes: 1);

        await auditLog.WriteAsync(AuditEvent.ToolExecDenied("a", "b", []));
        await auditLog.WriteAsync(AuditEvent.ToolExecDenied("c", "d", []));

        var files = Directory.GetFiles(_directory);
        Assert.Contains(files, f => Path.GetFileName(f) == "audit.jsonl");
        Assert.Contains(files, f => Path.GetFileName(f).StartsWith("audit-", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
