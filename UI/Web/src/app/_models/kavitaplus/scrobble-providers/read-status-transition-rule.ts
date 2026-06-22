import {PublicationStatus} from "../../metadata/publication-status";
import {ScrobbleReadStatus} from "./scrobble-read-status.enum";

export type ReadStatusTransitionRule = {
  enabled: boolean;
  days: number;
  transitionStatus: ScrobbleReadStatus;
  excludedPublicationStatus: PublicationStatus[];
}
