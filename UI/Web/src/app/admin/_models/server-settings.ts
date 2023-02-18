export interface ServerSettings {
    cacheDirectory: string;
    taskScan: string;
    taskBackup: string;
    loggingLevel: string;
    port: number;
    ipAddresses: string;
    allowStatCollection: boolean;
    enableOpds: boolean;
    baseUrl: string;
    bookmarksDirectory: string;
    emailServiceUrl: string;
    convertBookmarkToWebP: boolean;
    convertCoverToWebP: boolean;
    totalBackups: number;
    totalLogs: number;
    enableFolderWatching: boolean;
    hostName: string;
}
