import { Injectable, inject } from '@angular/core';
import {environment} from "../../environments/environment";
import {HttpClient} from "@angular/common/http";
import {EmailHistory} from "../_models/email-history";

@Injectable({
  providedIn: 'root'
})
export class EmailService {
  private httpClient = inject(HttpClient);

  baseUrl = environment.apiUrl;

  getEmailHistory() {
    return this.httpClient.get<EmailHistory[]>(`${this.baseUrl}email/all`);
  }
}
