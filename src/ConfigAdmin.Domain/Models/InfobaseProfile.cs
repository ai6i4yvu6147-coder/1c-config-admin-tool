using ConfigAdmin.Domain.Enums;

namespace ConfigAdmin.Domain.Models;

public sealed class InfobaseProfile
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PlatformPath { get; set; } = string.Empty;
    public ConnectionType ConnectionType { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public string? Username { get; set; }
    public byte[]? EncryptedPassword { get; set; }
    public bool ExportConfiguration { get; set; } = true;
    public bool ExportAllExtensions { get; set; } = true;
    public List<string> SelectedExtensions { get; set; } = [];
    public ExportFormat ExportFormat { get; set; } = ExportFormat.Hierarchical;
    public DateTimeOffset? LastExportAt { get; set; }
    public ExportStatus LastExportStatus { get; set; } = ExportStatus.Unknown;
    public Guid? ProjectId { get; set; }
    public Guid? ConfigMcpProjectId { get; set; }
    public ExportLocation ExportLocation { get; set; } = ExportLocation.Local;
    public Guid? RemoteNodeId { get; set; }
    public string? RemoteExportPath { get; set; }
}
