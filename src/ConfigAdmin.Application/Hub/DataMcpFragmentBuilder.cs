using ConfigAdmin.Domain.Hub;
using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Application.Hub;

public sealed class DataMcpFragmentBuilder
{
    public DataMcpRegistryFragmentDocument Build(
        DataMcpSettings settings,
        IReadOnlyList<DataConnection> connections)
    {
        var fragment = new DataMcpRegistryFragmentDocument
        {
            RegistryFragment = new DataMcpRegistryFragment
            {
                Yandex = new DataMcpYandexFragment
                {
                    Endpoint = settings.Endpoint,
                    Region = settings.Region,
                    Bucket = settings.Bucket,
                    DefaultPrefix = settings.DefaultPrefix
                },
                Connections = connections
                    .Where(c => !string.IsNullOrWhiteSpace(c.DatabaseId))
                    .Select(c => new DataMcpConnectionFragment
                    {
                        DataConnectionId = c.Id.ToString(),
                        InfobaseId = c.InfobaseId.ToString(),
                        DatabaseId = c.DatabaseId.Trim(),
                        DisplayName = string.IsNullOrWhiteSpace(c.DisplayName)
                            ? c.DatabaseId.Trim()
                            : c.DisplayName.Trim()
                    })
                    .ToList()
            }
        };

        return fragment;
    }
}
