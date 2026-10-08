namespace Kavita.Models.DTOs.SignalR.Bodies;

public sealed record BackupDatabaseProgressEventBody(float Progress);
public sealed record CleanupProgressEventBody(float Progress);
