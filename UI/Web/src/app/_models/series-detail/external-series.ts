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
  hardcoverId?: number;
  metadataProvider: MetadataProvider;
  recommendationSource: RecommendationSource;
}
