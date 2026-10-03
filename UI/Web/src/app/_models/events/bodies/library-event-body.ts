import {CodedEventBody} from './coded-event-body';

export interface LibraryEventBody extends CodedEventBody {
  libraryId: number;
  libraryName: string;
}
