import {Series} from "../series";
import {RecommendationSource} from "../kavitaplus/recommendation-source.enum";

/**
 * An owned (in-library) series surfaced as a recommendation, tagged with why it was recommended.
 */
export interface RecommendedSeries {
  series: Series;
  source: RecommendationSource;
}
