import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { of } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { ChapterMetadata } from '../_models/chapter-metadata';
import { AgeRating } from '../_models/metadata/age-rating';

@Injectable({
  providedIn: 'root'
})
export class MetadataService {

  baseUrl = environment.apiUrl;

  private ageRatingTypes: {[key: number]: string} | undefined = undefined;

  constructor(private httpClient: HttpClient) { }

  // getChapterMetadata(chapterId: number) {
  //   return this.httpClient.get<ChapterMetadata>(this.baseUrl + 'series/chapter-metadata?chapterId=' + chapterId);
  // }

  getAgeRating(ageRating: AgeRating) {
    if (this.ageRatingTypes != undefined && this.ageRatingTypes.hasOwnProperty(ageRating)) {
      return of(this.ageRatingTypes[ageRating]);
    }
    return this.httpClient.get<string>(this.baseUrl + 'series/age-rating?ageRating=' + ageRating, {responseType: 'text' as 'json'}).pipe(map(l => {
      if (this.ageRatingTypes === undefined) {
        this.ageRatingTypes = {};
      }

      this.ageRatingTypes[ageRating] = l;
      return this.ageRatingTypes[ageRating];
    }));
  }
}
