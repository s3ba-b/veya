using Xunit;

namespace Sage.McpServer.Tests;

public class ProgramTests
{
    [Fact]
    public void EntryPointAssembly_HasExpectedName()
    {
        // Smoke test: the project reference resolves and the assembly is wired
        // up under the conventional Sage.* name. Real MCP tool tests arrive
        // with the safety layer in a later issue.
        Assert.Equal("Sage.McpServer", typeof(Program).Assembly.GetName().Name);
    }
}
