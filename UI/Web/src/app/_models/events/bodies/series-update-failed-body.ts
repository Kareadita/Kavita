import {SeriesEventBody} from './series-event-body';

export interface SeriesUpdateFailedBody extends SeriesEventBody {
  /**
   * Written for users and shown as is. Null for an unexpected error
   */
  message: string | null;
}
