import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import {CollectionTag, UserCollection} from '../_models/collection-tag';
import { TextResonse } from '../_types/text-response';
import { ImageService } from './image.service';
import {MalStack} from "../_models/collection/mal-stack";

@Injectable({
  providedIn: 'root'
})
export class CollectionTagService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient, private imageService: ImageService) { }

  allCollections(ownedOnly = false) {
    return this.httpClient.get<UserCollection[]>(this.baseUrl + 'collection?ownedOnly=' + ownedOnly);
  }

  updateTag(tag: UserCollection) {
    return this.httpClient.post(this.baseUrl + 'collection/update', tag, TextResonse);
  }

  updateSeriesForTag(tag: CollectionTag, seriesIdsToRemove: Array<number>) {
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

  getMalStacks() {
    return this.httpClient.get<Array<MalStack>>(this.baseUrl + 'collection/mal-stacks');
  }
}
