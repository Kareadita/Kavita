/**
 * Represents a file being scanned during a Library Scan
 */
export interface FileScanProgressEvent {
  libraryId: number;
  libraryName: string;
  current: number | null;
  total: number | null;
  progress: number | null;
}
