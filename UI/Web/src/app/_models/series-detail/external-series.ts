import {ScrobbleProvider} from "../../_services/scrobbling.service";

export interface ExternalSeries {
  name: string;
  coverUrl: string;
  url: string;
  summary: string;
  aniListId?: number;
  malId?: number;
  provider: ScrobbleProvider;
}
