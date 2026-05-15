import {BookUploadConflictMode} from './book-upload-conflict-mode.enum';

export interface BookUploadRequest {
  libraryId: number;
  libraryFolder: string;
  targetFolderName?: string;
  conflictMode: BookUploadConflictMode;
}
