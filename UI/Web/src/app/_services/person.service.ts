import { Injectable } from '@angular/core';
import {HttpClient} from "@angular/common/http";
import {environment} from "../../environments/environment";
import {Person, PersonRole} from "../_models/metadata/person";

@Injectable({
  providedIn: 'root'
})
export class PersonService {

  baseUrl = environment.apiUrl;

  constructor(private httpClient: HttpClient) { }

  get(personId: number) {
    return this.httpClient.get<Person>(this.baseUrl + 'person/' + personId);
  }

  getRolesForPerson(personId: number) {
    return this.httpClient.get<Array<PersonRole>>(this.baseUrl + 'person/' + personId + '/roles');
  }
}
