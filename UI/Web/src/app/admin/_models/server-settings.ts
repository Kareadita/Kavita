export interface ServerSettings {
    cacheDirectory: string;
    taskScan: string;
    taskBackup: string;
    loggingLevel: string;
    port: number;
    allowStatCollection: boolean;
    enableOpds: boolean;
    enableAuthentication: boolean;
    baseUrl: string;
}
