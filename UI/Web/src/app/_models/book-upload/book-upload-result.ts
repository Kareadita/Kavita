export interface BookUploadFileResult {
  fileName: string;
  success: boolean;
  scanQueued: boolean;
  destinationPath?: string;
  error?: string;
}

export interface BookUploadResponse {
  files: BookUploadFileResult[];
  success: boolean;
  scanQueued: boolean;
}
