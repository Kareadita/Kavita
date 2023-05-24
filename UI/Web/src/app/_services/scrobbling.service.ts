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



}
