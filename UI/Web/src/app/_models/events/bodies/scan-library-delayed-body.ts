import {LibraryEventBody} from './library-event-body';

export interface ScanLibraryDelayedBody extends LibraryEventBody {
  scheduledForUtc: string;
}
