import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from 'src/environments/environment';
import { TextResonse } from '../_types/text-response';
import { ServerSettings } from './_models/server-settings';

/**
 * Used only for the Test Email Service call
 */
export interface EmailTestResult {
  successful: boolean;
  errorMessage: string;
}

@Injectable({
  providedIn: 'root'
})
export class SettingsService {

  baseUrl = environment.apiUrl;

  constructor(private http: HttpClient) { }

  getServerSettings() {
    return this.http.get<ServerSettings>(this.baseUrl + 'settings');
  }

  updateServerSettings(model: ServerSettings) {
    return this.http.post<ServerSettings>(this.baseUrl + 'settings', model);
  }

  resetServerSettings() {
    return this.http.post<ServerSettings>(this.baseUrl + 'settings/reset', {});
  }

  resetEmailServerSettings() {
    return this.http.post<ServerSettings>(this.baseUrl + 'settings/reset-email-url', {});
  }

  testEmailServerSettings(emailUrl: string) {
    return this.http.post<EmailTestResult>(this.baseUrl + 'settings/test-email-url', {url: emailUrl});
  }

  getTaskFrequencies() {
    return this.http.get<string[]>(this.baseUrl + 'settings/task-frequencies');
  }

  getLoggingLevels() {
    return this.http.get<string[]>(this.baseUrl + 'settings/log-levels');
  }

  getLibraryTypes() {
    return this.http.get<string[]>(this.baseUrl + 'settings/library-types');
  }

  getOpdsEnabled() {
    return this.http.get<boolean>(this.baseUrl + 'settings/opds-enabled', TextResonse);
  }
}
