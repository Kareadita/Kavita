import {HttpClient} from '@angular/common/http';
import {inject, Injectable} from '@angular/core';
import {environment} from 'src/environments/environment';
import {Member} from '../_models/auth/member';
import {UserTokenInfo} from "../_models/kavitaplus/user-token-info";
import {MemberInfo} from "../_models/user/member-info";
import {map} from "rxjs/operators";
import {TextResonse} from "../_types/text-response";

@Injectable({
  providedIn: 'root'
})
export class MemberService {
  private httpClient = inject(HttpClient);


  baseUrl = environment.apiUrl;

  getMembers(includePending: boolean = false) {
    return this.httpClient.get<Member[]>(this.baseUrl + 'users?includePending=' + includePending);
  }

  getMemberInfo(userId: number) {
    return this.httpClient.get<MemberInfo>(this.baseUrl + 'users/profile-info?userId=' + userId);
  }

  getMemberNames() {
    return this.httpClient.get<string[]>(this.baseUrl + 'users/names');
  }

  hasProfileShared(userId: number) {
    return this.httpClient.get<boolean>(this.baseUrl + 'users/has-profile-shared', TextResonse).pipe(map(d => (d + '') == 'true'));
  }

  getUserTokenInfo() {
    return this.httpClient.get<UserTokenInfo[]>(this.baseUrl + 'users/tokens');
  }

  adminExists() {
    return this.httpClient.get<boolean>(this.baseUrl + 'admin/exists');
  }

  deleteMember(username: string) {
    return this.httpClient.delete(this.baseUrl + 'users/delete-user?username=' + encodeURIComponent(username));
  }

  hasLibraryAccess(libraryId: number) {
    return this.httpClient.get<boolean>(this.baseUrl + 'users/has-library-access?libraryId=' + libraryId);
  }

  hasReadingProgress(libraryId: number) {
    return this.httpClient.get<boolean>(this.baseUrl + 'users/has-reading-progress?libraryId=' + libraryId);
  }

  addSeriesToWantToRead(seriesIds: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'want-to-read/add-series', {seriesIds});
  }

  removeSeriesToWantToRead(seriesIds: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'want-to-read/remove-series', {seriesIds});
  }
}
