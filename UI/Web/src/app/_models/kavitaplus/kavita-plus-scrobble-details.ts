import {LibraryType} from "../library/library";
import {ScrobbleProvider} from "../../_services/scrobbling.service";
import {ScrobbleEventType} from "../scrobbling/scrobble-event";

export interface KavitaPlusScrobbleDetails {
  scrobbleEventType: ScrobbleEventType | null;
  chapterNumber: number | null;
  volumeNumber: number | null;
  rating: number | null;
  provider: ScrobbleProvider;
  libraryType: LibraryType;
}
