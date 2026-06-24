import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {MetadataProvider} from "../kavitaplus/metadata-provider.enum";
import {RecommendationSource} from "../kavitaplus/recommendation-source.enum";

export interface ExternalSeries {
  name: string;
  coverUrl: string;
  url: string;
  summary: string;
  aniListId?: number;
  malId?: number;
  mangaBakaId?: number;
  /**
   * @deprecated Use metadataProvider instead
   */
  provider: ScrobbleProvider;
  metadataProvider: MetadataProvider;
  recommendationSource: RecommendationSource;
}
