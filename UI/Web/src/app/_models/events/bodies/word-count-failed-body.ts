import {SeriesEventBody} from './series-event-body';

export interface WordCountFailedBody extends SeriesEventBody {
  seriesId: number;
  filePath: string;
}
