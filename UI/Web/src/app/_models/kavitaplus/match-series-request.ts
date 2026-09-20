import {MetadataProvider} from "./metadata-provider.enum";

export interface MatchSeriesRequest {
  seriesId: number;
  query: string;
  isStandAlone: boolean;
  provider: MetadataProvider | null;
}
