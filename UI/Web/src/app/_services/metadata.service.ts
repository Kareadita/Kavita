import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { of } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from 'src/environments/environment';
import { ChapterMetadata } from '../_models/chapter-metadata';
import { Genre } from '../_models/genre';
import { AgeRating } from '../_models/metadata/age-rating';
import { Person } from '../_models/person';

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

  getAllGenres() {
    return this.httpClient.get<Genre[]>(this.baseUrl + 'metadata/genres');
  }

  getGenresForLibraries(libraries: Array<number>) {
    return this.httpClient.get<Genre[]>(this.baseUrl + 'metadata/genres?libraryIds=' + libraries.join(','));
  }

  getAllPeople() {
    return this.httpClient.get<Person[]>(this.baseUrl + 'metadata/people');
  }

  getPeopleForLibraries(libraries: Array<number>) {
    return this.httpClient.get<Person[]>(this.baseUrl + 'metadata/people?libraryIds=' + libraries.join(','));
  }
}
