import {Injectable} from '@angular/core';
import {HttpClient, HttpParams} from "@angular/common/http";
import {environment} from "../../environments/environment";
import {Person, PersonRole} from "../_models/metadata/person";
import {PaginatedResult} from "../_models/pagination";
import {Series} from "../_models/series";
import {map} from "rxjs/operators";
import {UtilityService} from "../shared/_services/utility.service";
import {BrowsePerson} from "../_models/metadata/browse/browse-person";
import {StandaloneChapter} from "../_models/standalone-chapter";
import {TextResonse} from "../_types/text-response";
import {FilterV2} from "../_models/metadata/v2/filter-v2";
import {PersonFilterField} from "../_models/metadata/v2/person-filter-field";
import {PersonSortField} from "../_models/metadata/v2/person-sort-field";

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

  searchPerson(name: string) {
    return this.httpClient.get<Array<Person>>(this.baseUrl + `person/search?queryString=${encodeURIComponent(name)}`);
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

  getAuthorsToBrowse(filter: FilterV2<PersonFilterField, PersonSortField>, pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.post<PaginatedResult<BrowsePerson[]>>(this.baseUrl + `person/all`, filter, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this.utilityService.createPaginatedResult(response) as PaginatedResult<BrowsePerson[]>;
      })
    );
  }

  // getAuthorsToBrowse(filter: BrowsePersonFilter, pageNum?: number, itemsPerPage?: number) {
  //   let params = new HttpParams();
  //   params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);
  //
  //   return this.httpClient.post<PaginatedResult<BrowsePerson[]>>(this.baseUrl + `person/all`, filter, {observe: 'response', params}).pipe(
  //     map((response: any) => {
  //       return this.utilityService.createPaginatedResult(response) as PaginatedResult<BrowsePerson[]>;
  //     })
  //   );
  // }

  downloadCover(personId: number) {
    return this.httpClient.post<string>(this.baseUrl + 'person/fetch-cover?personId=' + personId, {}, TextResonse);
  }

  isValidAlias(personId: number, alias: string) {
    return this.httpClient.get<boolean>(this.baseUrl + `person/valid-alias?personId=${personId}&alias=${alias}`, TextResonse).pipe(
      map(valid => valid + '' === 'true')
    );
  }

  mergePerson(destId: number, srcId: number) {
    return this.httpClient.post<Person>(this.baseUrl + 'person/merge', {destId, srcId});
  }

}
