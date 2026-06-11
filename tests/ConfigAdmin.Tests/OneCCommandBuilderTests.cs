using Xunit;
using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Integration;
using ConfigAdmin.Domain.Models;
using ConfigAdmin.Integration.OneC;

namespace ConfigAdmin.Tests;

public class OneCCommandBuilderTests
{
    [Fact]
    public void BuildDumpConfigCommand_ServerConnection_ContainsSAndDump()
    {
        var request = new DumpConfigRequest
        {
            Connection = new ConnectionSettings
            {
                ConnectionType = ConnectionType.Server,
                ConnectionString = @"srv01\erp",
                Username = "Admin",
                Password = "secret"
            },
            OutputPath = @"D:\Exports\Client\Base\Основная конфигурация"
        };

        var command = OneCCommandBuilder.BuildDumpConfigCommand(request);

        Assert.Contains("DESIGNER", command);
        Assert.Contains(@"/S ""srv01\erp""", command);
        Assert.Contains(@"/N""Admin""", command);
        Assert.Contains(@"/P""secret""", command);
        Assert.Contains("/DisableStartupDialogs", command);
        Assert.Contains("/DisableStartupMessages", command);
        Assert.Contains(@"/DumpConfigToFiles ""D:\Exports\Client\Base\Основная конфигурация""", command);
        Assert.Contains("-Format Hierarchical", command);
    }

    [Fact]
    public void BuildDumpConfigCommand_FileConnection_ContainsF()
    {
        var request = new DumpConfigRequest
        {
            Connection = new ConnectionSettings
            {
                ConnectionType = ConnectionType.File,
                ConnectionString = @"D:\Bases\Test",
                Username = "User"
            },
            OutputPath = @"D:\Out\Основная конфигурация"
        };

        var command = OneCCommandBuilder.BuildDumpConfigCommand(request);

        Assert.Contains(@"/F ""D:\Bases\Test""", command);
        Assert.DoesNotContain("/S", command);
    }

    [Fact]
    public void BuildDumpConfigCommand_AllExtensions_ContainsFlag()
    {
        var request = new DumpConfigRequest
        {
            Connection = new ConnectionSettings
            {
                ConnectionType = ConnectionType.Server,
                ConnectionString = "srv\\base"
            },
            OutputPath = @"D:\Out\Ext_Budget",
            AllExtensions = true
        };

        var command = OneCCommandBuilder.BuildDumpConfigCommand(request);

        Assert.Contains("-AllExtensions", command);
    }

    [Fact]
    public void BuildDumpConfigCommand_SingleExtension_ContainsExtensionName()
    {
        var request = new DumpConfigRequest
        {
            Connection = new ConnectionSettings
            {
                ConnectionType = ConnectionType.Server,
                ConnectionString = "srv\\base"
            },
            OutputPath = @"D:\Out\Ext_Budget",
            ExtensionName = "MyExt"
        };

        var command = OneCCommandBuilder.BuildDumpConfigCommand(request);

        Assert.Contains(@"-Extension ""MyExt""", command);
    }

    [Fact]
    public void MaskPassword_ReplacesPasswordValue()
    {
        const string command = @"DESIGNER /S ""srv\base"" /N""Admin"" /P""secret123"" /DumpConfigToFiles ""D:\out""";
        var masked = OneCCommandBuilder.MaskPassword(command);

        Assert.Contains(@"/P""***""", masked);
        Assert.DoesNotContain("secret123", masked);
    }

    [Fact]
    public void BuildTestConnectionCommand_ContainsDisableStartupFlags()
    {
        var profile = new InfobaseProfile
        {
            ConnectionType = ConnectionType.Server,
            ConnectionString = "srv\\base",
            Username = "Admin"
        };

        var command = OneCCommandBuilder.BuildTestConnectionCommand(profile, "pwd");

        Assert.Contains("/DisableStartupDialogs", command);
        Assert.Contains("/DisableStartupMessages", command);
    }

    [Fact]
    public void BuildDumpConfigCommand_OutAndDumpResult_ContainsParameters()
    {
        var request = new DumpConfigRequest
        {
            Connection = new ConnectionSettings
            {
                ConnectionType = ConnectionType.Server,
                ConnectionString = "srv\\base"
            },
            OutputPath = @"D:\Out\config",
            OutLogPath = @"D:\runs\step.out.log",
            DumpResultPath = @"D:\runs\step.dumpresult"
        };

        var command = OneCCommandBuilder.BuildDumpConfigCommand(request);

        Assert.Contains(@"/Out ""D:\runs\step.out.log""", command);
        Assert.Contains(@"/DumpResult ""D:\runs\step.dumpresult""", command);
    }
}
