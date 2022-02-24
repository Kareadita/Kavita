/**
 * Represents a file being scanned during a Library Scan
 */
export interface FileScanProgressEvent {
    // libraryId: number;
    // libraryName: string;
    // fileName: string;

    title: string;
    subtitle: string;
    eventTime: string;
}