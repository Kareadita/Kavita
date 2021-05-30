import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { CollectionTag } from '../_models/collection-tag';
import { ImageService } from './image.service';

@Injectable({
  providedIn: 'root'
})
export class CollectionTagService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient, private imageService: ImageService) { }

  allTags() {
    return this.httpClient.get<CollectionTag[]>(this.baseUrl + 'collection/').pipe(map(tags => {
      tags.forEach(s => s.coverImage = this.imageService.getCollectionCoverImage(s.id));
      return tags;
    }));
  }

  search(query: string) {
    return this.httpClient.get<CollectionTag[]>(this.baseUrl + 'collection/search?queryString=' + encodeURIComponent(query)).pipe(map(tags => {
      tags.forEach(s => s.coverImage = this.imageService.getCollectionCoverImage(s.id));
      return tags;
    }));
  }

  updateTag(tag: CollectionTag) {
    return this.httpClient.post(this.baseUrl + 'collection/update', tag, {responseType: 'text' as 'json'});
  }

  updateSeriesForTag(tag: CollectionTag, seriesIdsToRemove: Array<number>) {
    return this.httpClient.post(this.baseUrl + 'collection/update-series', {tag, seriesIdsToRemove}, {responseType: 'text' as 'json'});
  }
}
