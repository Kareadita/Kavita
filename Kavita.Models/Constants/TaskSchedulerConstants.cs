namespace Kavita.Models.Constants;

public static class TaskSchedulerConstants
{
    public const string ScanQueue = "scan";
    public const string DefaultQueue = "default";
    public const string RemoveFromWantToReadTaskId = "remove-from-want-to-read";
    public const string UpdateYearlyStatsTaskId = "update-yearly-stats";
    public const string SyncThemesTaskId = "sync-themes";
    public const string CheckForUpdateId = "check-updates";
    public const string CleanupDbTaskId = "cleanup-db";
    public const string CleanupTaskId = "cleanup";
    public const string TaskCblSyncId = "sync-cbl";
    public const string BackupTaskId = "backup";
    public const string ScanLibrariesTaskId = "scan-libraries";
    public const string ReportStatsTaskId = "report-stats";
    public const string CheckScrobblingTokensId = "kavita+-check-scrobbling-tokens";
    public const string ProcessScrobblingEventsId = "kavita+-process-scrobbling-events";
    public const string ProcessProcessedScrobblingEventsId = "kavita+-process-processed-scrobbling-events";
    public const string LicenseCheckId = "license-check";
    public const string KavitaPlusDataRefreshId = "kavita+-data-refresh";
    public const string KavitaPlusStackSyncId = "kavita+-stack-sync";
    public const string KavitaPlusWantToReadSyncId = "kavita+-want-to-read-sync";
    public const string ReadingHistoryAggregationId = "reading-history-aggregation";
    public const string AuthKeyExpirationId = "auth-key-expiration";
    public const string EnsureSideNavId = "ensure-sidenav";
    public const string FlushUserActiveTaskId = "flush-user-active";
    public const string PurgeKavitaPlusAuditLogsId = "kavita+-purge-audit-logs";
    public const string CreateReadStatusTransitionRuleEventsId = "kavita+-create-read-status-transition-rule-events";
    public const string RefreshConnectedTokensId = "kavita+-refresh-connected-tokens";
}
