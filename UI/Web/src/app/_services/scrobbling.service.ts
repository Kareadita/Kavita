import { HttpClient } from '@angular/common/http';
import {DestroyRef, inject, Injectable, OnDestroy} from '@angular/core';
import { of, ReplaySubject, Subject } from 'rxjs';
import { filter, map, switchMap, takeUntil } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { Preferences } from '../_models/preferences/preferences';
import { User } from '../_models/user';
import { Router } from '@angular/router';
import { EVENTS, MessageHubService } from './message-hub.service';
import { ThemeService } from './theme.service';
import { InviteUserResponse } from '../_models/auth/invite-user-response';
import { UserUpdateEvent } from '../_models/events/user-update-event';
import { UpdateEmailResponse } from '../_models/auth/update-email-response';
import { AgeRating } from '../_models/metadata/age-rating';
import { AgeRestriction } from '../_models/metadata/age-restriction';
import { TextResonse } from '../_types/text-response';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ScrobbleError} from "../_models/scrobbling/scrobble-error";
import {ScrobbleEvent} from "../_models/scrobbling/scrobble-event";
import {ScrobbleHold} from "../_models/scrobbling/scrobble-hold";

export enum ScrobbleProvider {
  AniList= 1
}

@Injectable({
  providedIn: 'root'
})
export class ScrobblingService {

  private readonly destroyRef = inject(DestroyRef);
  baseUrl = environment.apiUrl;


  constructor(private httpClient: HttpClient) {}

  hasTokenExpired(provider: ScrobbleProvider) {
    return this.httpClient.get<string>(this.baseUrl + 'scrobbling/token-expired?provider=' + provider, TextResonse)
      .pipe(map(r => r === "true"));
  }


  updateAniListToken(token: string) {
    return this.httpClient.post(this.baseUrl + 'scrobbling/update-anilist-token', {token});
  }

  getAniListToken() {
    return this.httpClient.get<string>(this.baseUrl + 'scrobbling/anilist-token', TextResonse);
  }


  getScrobbleErrors() {
    return this.httpClient.get<Array<ScrobbleError>>(this.baseUrl + 'scrobbling/scrobble-errors');
  }
  getScrobbleEvents() {
    return this.httpClient.get<Array<ScrobbleEvent>>(this.baseUrl + 'scrobbling/scrobble-events');
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
}
