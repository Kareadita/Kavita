import {ExternalSeriesMatch} from "./external-series-match";
import {MetadataProvider} from "../kavitaplus/metadata-provider.enum";

export interface MatchSeriesResult {
  /**
   * The provider the search was actually performed against. Can differ from the series' current provider when a
   * provider specific url/header was queried, or another provider was explicitly requested
   */
  provider: MetadataProvider;
  matches: Array<ExternalSeriesMatch>;
}
