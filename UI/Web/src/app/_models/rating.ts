import {ScrobbleProvider} from "../_services/scrobbling.service";

export interface Rating {
  averageScore: number;
  meanScore: number;
  favoriteCount: number;
  provider: ScrobbleProvider;
  providerUrl: string | undefined;
}
