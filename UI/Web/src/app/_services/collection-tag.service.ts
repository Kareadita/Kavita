import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { CollectionTag } from '../_models/collection-tag';
import { TextResonse } from '../_types/text-response';
import { ImageService } from './image.service';

@Injectable({
  providedIn: 'root'
})
export class CollectionTagService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient, private imageService: ImageService) { }

  allTags() {
    return this.httpClient.get<CollectionTag[]>(this.baseUrl + 'collection/');
  }

  search(query: string) {
    return this.httpClient.get<CollectionTag[]>(this.baseUrl + 'collection/search?queryString=' + encodeURIComponent(query)).pipe(map(tags => {
      tags.forEach(s => s.coverImage = this.imageService.randomize(this.imageService.getCollectionCoverImage(s.id)));
      return tags;
    }));
  }

  updateTag(tag: CollectionTag) {
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
}
