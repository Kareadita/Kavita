import {FileTypeGroup} from '../library/file-type-group.enum';

export interface BookUploadOptions {
  libraryId: number;
  libraryFolders: string[];
  libraryFileTypes: FileTypeGroup[];
  acceptableExtensions: string[];
  maxUploadSizeBytes: number;
}
