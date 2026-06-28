using ConfigAdmin.Infrastructure.Hub;
using System.Text;
using Xunit;

namespace ConfigAdmin.Tests.Hub;

public class JsonCliRunnerEncodingTests
{
    [Fact]
    public void DecodeProcessOutput_ReadsUtf8Cyrillic()
    {
        var bytes = Encoding.UTF8.GetBytes("Планета");
        var decoded = JsonCliRunner.DecodeProcessOutput(bytes);

        Assert.Equal("Планета", decoded);
    }

    [Fact]
    public void DecodeProcessOutput_MatchesConfigMcpCliEncoding()
    {
        // Bytes observed from config-mcp status --json after UTF-8 fix
        var bytes = new byte[] { 0xD0, 0x9F, 0xD0, 0xBB, 0xD0, 0xB0, 0xD0, 0xBD, 0xD0, 0xB5, 0xD1, 0x82, 0xD0, 0xB0 };
        var decoded = JsonCliRunner.DecodeProcessOutput(bytes);

        Assert.Equal("Планета", decoded);
    }
}
