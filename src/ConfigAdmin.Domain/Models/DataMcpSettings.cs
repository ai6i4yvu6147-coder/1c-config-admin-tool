namespace ConfigAdmin.Domain.Models;

public sealed class DataMcpSettings
{
    public Guid ToolInstanceId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public string DefaultPrefix { get; set; } = string.Empty;
    public string SealedSecretsPath { get; set; } = "credentials.sealed.json";
    public byte[]? EncryptedDmcpPassword { get; set; }
}
