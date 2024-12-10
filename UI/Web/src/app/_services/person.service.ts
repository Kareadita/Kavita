import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from "@angular/common/http";
import {environment} from "../../environments/environment";
import {Person, PersonRole} from "../_models/metadata/person";
import {SeriesFilterV2} from "../_models/metadata/v2/series-filter-v2";
import {PaginatedResult} from "../_models/pagination";
import {Series} from "../_models/series";
import {map} from "rxjs/operators";
import {UtilityService} from "../shared/_services/utility.service";
import {BrowsePerson} from "../_models/person/browse-person";
import {Chapter} from "../_models/chapter";
import {StandaloneChapter} from "../_models/standalone-chapter";
import {TextResonse} from "../_types/text-response";

@Injectable({
  providedIn: 'root'
})
export class PersonService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient, private utilityService: UtilityService) { }

  updatePerson(person: Person) {
    return this.httpClient.post<Person>(this.baseUrl + "person/update", person);
  }

  get(name: string) {
    return this.httpClient.get<Person | null>(this.baseUrl + `person?name=${name}`);
  }

  getRolesForPerson(personId: number) {
    return this.httpClient.get<Array<PersonRole>>(this.baseUrl + `person/roles?personId=${personId}`);
  }

  getSeriesMostKnownFor(personId: number) {
    return this.httpClient.get<Array<Series>>(this.baseUrl + `person/series-known-for?personId=${personId}`);
  }

  getChaptersByRole(personId: number, role: PersonRole) {
    return this.httpClient.get<Array<StandaloneChapter>>(this.baseUrl + `person/chapters-by-role?personId=${personId}&role=${role}`);
  }

  getAuthorsToBrowse(pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.post<PaginatedResult<BrowsePerson[]>>(this.baseUrl + 'person/all', {}, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this.utilityService.createPaginatedResult(response) as PaginatedResult<BrowsePerson[]>;
      })
    );
  }

  downloadCover(personId: number) {
    return this.httpClient.post<string>(this.baseUrl + 'person/fetch-cover?personId=' + personId, {}, TextResonse);
  }
}
