import {LibraryEventBody} from './library-event-body';

export interface UnreadableFoldersBody extends LibraryEventBody {
  /**
   * The first 10, folderCount has the total
   */
  folders: string[];
  folderCount: number;
}
