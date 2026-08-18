import {Series} from "../series";

export interface ManageMatchSeries {
  series: Series;
  isMatched: boolean;
  validUntilUtc: string;
}

export interface MatchedExternalSeriesCount {
  totalCount: number;
  dontMatchCount: number;
  notMatchedCount: number;
  erroredCount: number;
}
