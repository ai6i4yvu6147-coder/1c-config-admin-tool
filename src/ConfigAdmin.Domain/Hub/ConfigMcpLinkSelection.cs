namespace ConfigAdmin.Domain.Hub;

public sealed class ConfigMcpLinkSelection
{
    public bool CreateNewProject { get; init; }
    public Guid? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public bool CreateNewDatabase { get; init; }
    public Guid? DatabaseId { get; init; }
}
