import {Series} from "../series";

export interface ManageMatchSeries {
  series: Series;
  isMatched: boolean;
  validUntilUtc: string;
}
