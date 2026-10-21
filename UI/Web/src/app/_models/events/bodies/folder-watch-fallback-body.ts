import {LibraryEventBody} from './library-event-body';
import {FolderWatchFallbackReason} from '../core/folder-watch-fallback-reason';

export interface FolderWatchFallbackBody extends LibraryEventBody {
  folder: string;
  reason: FolderWatchFallbackReason;
}
