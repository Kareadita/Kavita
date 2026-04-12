import {HttpClient} from '@angular/common/http';
import {inject, Injectable} from '@angular/core';
import {environment} from 'src/environments/environment';
import {UserCollection} from '../_models/collection-tag';
import {TextResonse} from '../_types/text-response';
import {MalStack} from "../_models/collection/mal-stack";
import {User} from "../_models/user/user";
import {AccountService, Role} from "./account.service";
import {ActionItem} from "../_models/actionables/action-item";
import {Action} from "../_models/actionables/action";

@Injectable({
  providedIn: 'root'
})
export class CollectionTagService {
  private httpClient = inject(HttpClient);
  private accountService = inject(AccountService);


  baseUrl = environment.apiUrl;

  getCollectionById(collectionId: number) {
    return this.httpClient.get<UserCollection>(this.baseUrl + 'collection/single?collectionId=' + collectionId);
  }

  allCollections(ownedOnly = false, sortByLastModified = false) {
    return this.httpClient.get<UserCollection[]>(this.baseUrl + `collection?ownedOnly=${ownedOnly}&sortByLastModified=${sortByLastModified}`);
  }

  allCollectionsForSeries(seriesId: number, ownedOnly = false) {
    return this.httpClient.get<UserCollection[]>(this.baseUrl + 'collection/all-series?ownedOnly=' + ownedOnly + '&seriesId=' + seriesId);
  }

  updateTag(tag: UserCollection) {
    return this.httpClient.post<UserCollection>(this.baseUrl + 'collection/update', tag);
  }

  promoteMultipleCollections(tags: Array<number>, promoted: boolean) {
    return this.httpClient.post(this.baseUrl + 'collection/promote-multiple', {collectionIds: tags, promoted}, TextResonse);
  }

  updateSeriesForTag(tag: UserCollection, seriesIdsToRemove: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'collection/update-series', {tag, seriesIdsToRemove}, TextResonse);
  }

  addByMultiple(tagId: number, seriesIds: Array<number>, tagTitle: string = '') {
    return this.httpClient.post(this.baseUrl + 'collection/update-for-series', {collectionTagId: tagId, collectionTagTitle: tagTitle, seriesIds}, TextResonse);
  }

  tagNameExists(name: string) {
    return this.httpClient.get<boolean>(this.baseUrl + 'collection/name-exists?name=' + name);
  }

  deleteTag(tagId: number) {
    return this.httpClient.delete<string>(this.baseUrl + 'collection?tagId=' + tagId, TextResonse);
  }

  deleteMultipleCollections(tags: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'collection/delete-multiple', {collectionIds: tags}, TextResonse);
  }

  getMalStacks() {
    return this.httpClient.get<Array<MalStack>>(this.baseUrl + 'collection/mal-stacks');
  }

  actionListFilter(action: ActionItem<UserCollection>, user: User) {
    const canPromote = this.accountService.hasRole(user, Role.Admin) || this.accountService.hasRole(user, Role.Promote);
    const isPromotionAction = action.action == Action.Promote || action.action == Action.UnPromote;

    if (isPromotionAction) return canPromote;
    return true;
  }

  importStack(stack: MalStack) {
    return this.httpClient.post(this.baseUrl + 'collection/import-stack', stack, TextResonse);
  }
}
