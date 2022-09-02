export interface ServerSettings {
    cacheDirectory: string;
    taskScan: string;
    taskBackup: string;
    loggingLevel: string;
    port: number;
    allowStatCollection: boolean;
    enableOpds: boolean;
    baseUrl: string;
    bookmarksDirectory: string;
    emailServiceUrl: string;
    convertBookmarkToWebP: boolean;
    enableSwaggerUi: boolean;
    totalBackups: number;
    enableFolderWatching: boolean;
}
