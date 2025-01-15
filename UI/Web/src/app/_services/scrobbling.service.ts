import { HttpClient, HttpParams } from '@angular/common/http';
import {Injectable} from '@angular/core';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { TextResonse } from '../_types/text-response';
import {ScrobbleError} from "../_models/scrobbling/scrobble-error";
import {ScrobbleEvent} from "../_models/scrobbling/scrobble-event";
import {ScrobbleHold} from "../_models/scrobbling/scrobble-hold";
import {PaginatedResult} from "../_models/pagination";
import {ScrobbleEventFilter} from "../_models/scrobbling/scrobble-event-filter";
import {UtilityService} from "../shared/_services/utility.service";

export enum ScrobbleProvider {
  Kavita = 0,
  AniList= 1,
  Mal = 2,
  GoogleBooks = 3
}

@Injectable({
  providedIn: 'root'
})
export class ScrobblingService {

  baseUrl = environment.apiUrl;


  constructor(private httpClient: HttpClient, private utilityService: UtilityService) {}

  hasTokenExpired(provider: ScrobbleProvider) {
    return this.httpClient.get<string>(this.baseUrl + 'scrobbling/token-expired?provider=' + provider, TextResonse)
      .pipe(map(r => r === "true"));
  }

  /**
   * Returns if the token was new or not
   */
  updateAniListToken(token: string) {
    return this.httpClient.post<boolean>(this.baseUrl + 'scrobbling/update-anilist-token', {token}, TextResonse)
      .pipe(map(r => r + '' === 'true'));
  }

  /**
   * Returns if the token was new or not
   */
  updateMalToken(username: string, accessToken: string) {
    return this.httpClient.post<boolean>(this.baseUrl + 'scrobbling/update-mal-token', {username, accessToken}, TextResonse)
      .pipe(map(r => r + '' === 'true'));
  }

  getAniListToken() {
    return this.httpClient.get<string>(this.baseUrl + 'scrobbling/anilist-token', TextResonse);
  }

  getMalToken() {
    return this.httpClient.get<{username: string, accessToken: string}>(this.baseUrl + 'scrobbling/mal-token');
  }

  getScrobbleErrors() {
    return this.httpClient.get<Array<ScrobbleError>>(this.baseUrl + 'scrobbling/scrobble-errors');
  }

  getScrobbleEvents(filter: ScrobbleEventFilter, pageNum: number | undefined = undefined, itemsPerPage: number | undefined = undefined) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);
    return this.httpClient.post<PaginatedResult<ScrobbleEvent[]>>(this.baseUrl + 'scrobbling/scrobble-events', filter, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this.utilityService.createPaginatedResult(response, new PaginatedResult<ScrobbleEvent[]>());
      })
    );
  }

  clearScrobbleErrors() {
    return this.httpClient.post(this.baseUrl + 'scrobbling/clear-errors', {});
  }

  getHolds() {
    return this.httpClient.get<Array<ScrobbleHold>>(this.baseUrl + 'scrobbling/holds');
  }

  libraryAllowsScrobbling(seriesId: number) {
    return this.httpClient.get(this.baseUrl + 'scrobbling/library-allows-scrobbling?seriesId=' + seriesId, TextResonse)
      .pipe(map(res => res === "true"));
  }

  hasHold(seriesId: number) {
    return this.httpClient.get(this.baseUrl + 'scrobbling/has-hold?seriesId=' + seriesId, TextResonse)
      .pipe(map(res => res === "true"));
  }

  addHold(seriesId: number) {
    return this.httpClient.post(this.baseUrl + 'scrobbling/add-hold?seriesId=' + seriesId, TextResonse);
  }

  removeHold(seriesId: number) {
    return this.httpClient.delete(this.baseUrl + 'scrobbling/remove-hold?seriesId=' + seriesId, TextResonse);
  }

  triggerScrobbleEventGeneration() {
    return this.httpClient.post(this.baseUrl + 'scrobbling/generate-scrobble-events', TextResonse);

  }
}
