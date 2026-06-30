namespace ConfigAdmin.Domain.Hub;

public enum ConfigMcpLinkMode
{
    ExistingDatabase,
    NewDatabaseInProject,
    NewProject
}

public sealed class ConfigMcpLinkRequest
{
    public ConfigMcpLinkMode Mode { get; init; }
    public Guid? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public Guid? DatabaseId { get; init; }
}
