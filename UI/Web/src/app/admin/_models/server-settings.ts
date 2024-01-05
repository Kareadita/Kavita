import { EncodeFormat } from "./encode-format";
import {CoverImageSize} from "./cover-image-size";

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
    encodeMediaAs: EncodeFormat;
    totalBackups: number;
    totalLogs: number;
    enableFolderWatching: boolean;
    hostName: string;
    cacheSize: number;
    onDeckProgressDays: number;
    onDeckUpdateDays: number;
    coverImageSize: CoverImageSize;
    customHeaderWhitelistIpRanges: string;
}
