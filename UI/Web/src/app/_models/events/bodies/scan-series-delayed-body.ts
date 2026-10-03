import {SeriesEventBody} from './series-event-body';

export interface ScanSeriesDelayedBody extends SeriesEventBody {
  seriesId: number;
  scheduledForUtc: string;
}
