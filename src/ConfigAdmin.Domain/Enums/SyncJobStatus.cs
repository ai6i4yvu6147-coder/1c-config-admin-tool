namespace ConfigAdmin.Domain.Enums;

public enum SyncJobStatus
{
    Pending = 0,
    Claimed = 1,
    Exporting = 2,
    Uploading = 3,
    Applying = 4,
    Completed = 5,
    Failed = 6,
    Cancelled = 7
}
