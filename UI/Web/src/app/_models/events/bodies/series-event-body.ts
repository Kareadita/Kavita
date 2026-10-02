import {CodedEventBody} from './coded-event-body';

export interface SeriesEventBody extends CodedEventBody {
  libraryId: number;
  /**
   * Null when the series was new and never saved
   */
  seriesId: number | null;
  seriesName: string;
}
