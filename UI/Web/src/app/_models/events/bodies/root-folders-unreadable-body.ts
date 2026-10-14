import {LibraryEventBody} from './library-event-body';

export interface RootFoldersUnreadableBody extends LibraryEventBody {
  folders: string[];
}
