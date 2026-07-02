using ConfigAdmin.Application.Hub;
using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Domain.Models;
using Xunit;

namespace ConfigAdmin.Tests.Hub;

public class DataMcpFragmentBuilderTests
{
    [Fact]
    public void Build_IncludesYandexAndPairedConnectionsOnly()
    {
        var builder = new DataMcpFragmentBuilder();
        var settings = new DataMcpSettings
        {
            ToolInstanceId = Guid.NewGuid(),
            Endpoint = "https://storage.yandexcloud.net",
            Region = "ru-central1",
            Bucket = "bucket-1",
            DefaultPrefix = "exchange"
        };

        var connections = new List<DataConnection>
        {
            new()
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                InfobaseId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                DatabaseId = "a1b2c3d4",
                DisplayName = "ERP"
            },
            new()
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                InfobaseId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                DatabaseId = string.Empty,
                DisplayName = "Unpaired"
            }
        };

        var fragment = builder.Build(settings, connections);

        Assert.Equal(HubModuleIds.DataMcp, fragment.ModuleId);
        Assert.Equal("bucket-1", fragment.RegistryFragment.Yandex.Bucket);
        Assert.Single(fragment.RegistryFragment.Connections);
        Assert.Equal("a1b2c3d4", fragment.RegistryFragment.Connections[0].DatabaseId);
        Assert.Equal("11111111-1111-1111-1111-111111111111", fragment.RegistryFragment.Connections[0].DataConnectionId);
    }
}
