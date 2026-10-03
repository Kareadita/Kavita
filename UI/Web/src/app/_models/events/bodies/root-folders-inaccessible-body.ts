import {LibraryEventBody} from './library-event-body';

export interface RootFoldersInaccessibleBody extends LibraryEventBody {
  folders: string[];
}
