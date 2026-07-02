import {ExternalSeries} from "./external-series";
import {RecommendedSeries} from "./recommended-series";

export interface Recommendation {
  ownedSeries: Array<RecommendedSeries>;
  externalSeries: Array<ExternalSeries>;
}
