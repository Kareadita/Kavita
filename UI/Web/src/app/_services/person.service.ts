import { Injectable } from '@angular/core';
import {HttpClient, HttpParams} from "@angular/common/http";
import {environment} from "../../environments/environment";
import {Person, PersonRole} from "../_models/metadata/person";
import {SeriesFilterV2} from "../_models/metadata/v2/series-filter-v2";
import {PaginatedResult} from "../_models/pagination";
import {Series} from "../_models/series";
import {map} from "rxjs/operators";
import {UtilityService} from "../shared/_services/utility.service";
import {BrowsePerson} from "../_models/person/browse-person";

@Injectable({
  providedIn: 'root'
})
export class PersonService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient, private utilityService: UtilityService) { }

  get(personId: number) {
    return this.httpClient.get<Person>(this.baseUrl + 'person/' + personId);
  }

  getRolesForPerson(personId: number) {
    return this.httpClient.get<Array<PersonRole>>(this.baseUrl + 'person/' + personId + '/roles');
  }


  getAuthorsToBrowse(pageNum?: number, itemsPerPage?: number) {
    let params = new HttpParams();
    params = this.utilityService.addPaginationIfExists(params, pageNum, itemsPerPage);

    return this.httpClient.post<PaginatedResult<BrowsePerson[]>>(this.baseUrl + 'person/authors', {}, {observe: 'response', params}).pipe(
      map((response: any) => {
        return this.utilityService.createPaginatedResult(response) as PaginatedResult<BrowsePerson[]>;
      })
    );
  }
}
