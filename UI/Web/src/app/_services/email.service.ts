import { Injectable } from '@angular/core';
import {environment} from "../../environments/environment";
import {HttpClient} from "@angular/common/http";
import {EmailHistory} from "../_models/email-history";

@Injectable({
  providedIn: 'root'
})
export class EmailService {
  baseUrl = environment.apiUrl;
  constructor(private httpClient: HttpClient) { }

  getEmailHistory() {
    return this.httpClient.get<EmailHistory[]>(`${this.baseUrl}email/all`);
  }
}
