import {Series} from "../series";
import {ExternalSeries} from "./external-series";

export interface Recommendation {
  ownedSeries: Array<Series>;
  externalSeries: Array<ExternalSeries>;
}
