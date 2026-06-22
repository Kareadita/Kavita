import {HttpClient, HttpParams} from '@angular/common/http';
import {inject, Injectable} from '@angular/core';
import {map} from 'rxjs/operators';
import {environment} from 'src/environments/environment';
import {TextResonse} from '../_types/text-response';
import {ScrobbleError} from "../_models/scrobbling/scrobble-error";
import {ScrobbleEvent} from "../_models/scrobbling/scrobble-event";
import {ScrobbleHold} from "../_models/scrobbling/scrobble-hold";
import {PaginatedResult} from "../_models/pagination";
import {ScrobbleEventFilter} from "../_models/scrobbling/scrobble-event-filter";
import {UtilityService} from "../shared/_services/utility.service";
import {KavitaPlusAuditEntry} from "../_models/kavitaplus/kavita-plus-audit-entry";
import {ScrobbleProviderSettings} from "../_models/kavitaplus/scrobble-providers/scrobble-provider-settings";
import {UpdateScrobbleProvider} from "../_models/kavitaplus/scrobble-providers/update-scrobble-provider";
import {UserScrobbleProvider} from "../_models/kavitaplus/scrobble-providers/user-scrobble-provider";

export enum ScrobbleProvider {
  Kavita = 0,
  AniList = 1,
  Mal = 2,
  Cbr = 4,
  Hardcover = 5,
  MangaBaka = 6,
}

@Injectable({
  providedIn: 'root'
})
export class ScrobblingService {
  private httpClient = inject(HttpClient);
  private utilityService = inject(UtilityService);

  baseUrl = environment.apiUrl;

  validExternalScrobbleProviders() {
    return [ScrobbleProvider.AniList, ScrobbleProvider.Mal, ScrobbleProvider.Hardcover, ScrobbleProvider.MangaBaka]
  }

  getScrobbleProviders() {
    return this.httpClient.get<UserScrobbleProvider[]>(this.baseUrl + 'scrobbling/scrobble-settings').pipe(
      map(providers => providers.map(p => UserScrobbleProvider.From(p)))
    );
  }

  getNextScrobble() {
    return this.httpClient.get<string | null>(this.baseUrl + 'scrobbling/next-scrobble-time', TextResonse).pipe(map(res => {
      // For some reason, sending a Raw DateTime puts quotes around it
      if (res && res.startsWith('"')) {
        return res.replaceAll('"', '');
      }

      return res;
    }));
  }

  saveScrobbleSettings(provider: ScrobbleProvider, settings: ScrobbleProviderSettings) {
    return this.httpClient.post(this.baseUrl + 'scrobbling/update-scrobble-settings?provider=' + provider, settings);
  }

  saveUserScrobbleProvider(updateDto: UpdateScrobbleProvider) {
    return this.httpClient.post(this.baseUrl + 'scrobbling/update-user-scrobble-provider', updateDto);
  }

  hasTokenExpired(provider: ScrobbleProvider) {
    return this.httpClient.get<string>(this.baseUrl + 'scrobbling/token-expired?provider=' + provider, TextResonse).pipe(
      map(s => s === 'true')
    );
  }

  checkExpiredTokens() {
    return this.httpClient.get<ScrobbleProvider[]>(this.baseUrl + 'scrobbling/expired-tokens');
  }

  /**
   * Re-queues the underlying event to process. Only applicable if the event is in failed/rate limit state
   * @param event
   */
  retryScrobbleEvent(event: KavitaPlusAuditEntry) {
    return this.httpClient.post(this.baseUrl + 'scrobbling/retry-scrobble', event, TextResonse).pipe(map(r => r === 'true'));
  }

  hasRunScrobbleGen() {
    return this.httpClient.get(this.baseUrl + 'scrobbling/has-ran-scrobble-gen ', TextResonse).pipe(map(r => r === 'true'));
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

  triggerScrobbleEventGeneration(provider: ScrobbleProvider) {
    return this.httpClient.post(this.baseUrl + 'scrobbling/generate-scrobble-events?scrobbleProvider=' + provider, TextResonse);
  }

  triggerScrobbleEventGenerationForAllValid() {
    return this.httpClient.post<string>(this.baseUrl + 'scrobbling/generate-scrobble-events-all', {}, TextResonse).pipe(
      map(s => s === 'true')
    );
  }

  bulkRemoveEvents(eventIds: number[]) {
    return this.httpClient.post(this.baseUrl + "scrobbling/bulk-remove-events", eventIds)
  }

}
