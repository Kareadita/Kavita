import {LibraryType} from "../library/library";
import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {ScrobbleEventType} from "../scrobbling/scrobble-event";
import {ScrobbleReadStatus} from "./scrobble-providers/scrobble-read-status.enum";

export interface KavitaPlusScrobbleDetails {
  scrobbleEventType: ScrobbleEventType | null;
  chapterNumber: number | null;
  volumeNumber: number | null;
  percentRead: number | null;
  rating: number | null;
  provider: ScrobbleProvider;
  libraryType: LibraryType;
  readStatus: ScrobbleReadStatus | null;
}
